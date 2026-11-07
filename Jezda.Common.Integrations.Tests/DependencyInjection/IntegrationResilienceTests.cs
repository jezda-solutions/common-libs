using System.Net;
using Jezda.Common.Integrations.Abstractions.Resilience;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;
using Xunit;

namespace Jezda.Common.Integrations.Tests.DependencyInjection;

/// <summary>
/// Runs requests through the real resilience pipeline, which the provider tests bypass.
/// </summary>
public class IntegrationResilienceTests
{
    private const string ClientName = "resilience-test";

    private readonly MockHttpMessageHandler _handler = new();

    private HttpClient CreateClient()
    {
        var services = new ServiceCollection();
        services.AddHttpClient(ClientName, c => c.BaseAddress = new Uri("https://api.example.test/"))
            .ConfigurePrimaryHttpMessageHandler(() => _handler)
            .AddIntegrationResilience();

        return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);
    }

    [Fact]
    public async Task TooManyRequests_LongRetryAfter_IsNotRetried()
    {
        // A retry would wait out Retry-After past the total timeout and turn the 429 into a timeout.
        _handler.EnqueueResponse(() => TooManyRequests(retryAfterSeconds: 60));
        _handler.EnqueueResponse(HttpStatusCode.OK);

        using var response = await CreateClient().GetAsync("items");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.RetryAfter?.Delta);
        Assert.Single(_handler.SentRequests);
    }

    [Fact]
    public async Task TooManyRequests_WithoutRetryAfter_IsNotRetried()
    {
        _handler.EnqueueResponse(() => TooManyRequests(retryAfterSeconds: null));
        _handler.EnqueueResponse(HttpStatusCode.OK);

        using var response = await CreateClient().GetAsync("items");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Single(_handler.SentRequests);
    }

    [Fact]
    public async Task TooManyRequests_ShortRetryAfter_IsRetried()
    {
        // A short pause, as Azure DevOps and Jira throttling ask for, is still waited out quietly.
        _handler.EnqueueResponse(() => TooManyRequests(retryAfterSeconds: 1));
        _handler.EnqueueResponse(HttpStatusCode.OK);

        using var response = await CreateClient().GetAsync("items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, _handler.SentRequests.Count);
    }

    private static HttpResponseMessage TooManyRequests(int? retryAfterSeconds)
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{}") };
        if (retryAfterSeconds is { } seconds)
        {
            response.Headers.Add("Retry-After", seconds.ToString());
        }

        return response;
    }

    [Fact]
    public async Task TooManyRequests_DoNotOpenTheSharedCircuitBreaker()
    {
        // The breaker is shared by every token on the named client, so one token's rate limit must
        // not cut off the others. 100 is the breaker's minimum throughput; all of them fail.
        var client = CreateClient();
        for (var i = 0; i < 120; i++)
        {
            _handler.EnqueueResponse(() => TooManyRequests(retryAfterSeconds: 60));
        }

        _handler.EnqueueResponse(HttpStatusCode.OK);

        for (var i = 0; i < 120; i++)
        {
            using var limited = await client.GetAsync("items");
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        }

        using var response = await client.GetAsync("items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ServiceUnavailable_IsStillRetried()
    {
        _handler.EnqueueResponse(HttpStatusCode.ServiceUnavailable);
        _handler.EnqueueResponse(HttpStatusCode.OK);

        using var response = await CreateClient().GetAsync("items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, _handler.SentRequests.Count);
    }

    [Fact]
    public async Task ServerErrors_StillOpenTheCircuitBreaker()
    {
        // Each call is tried four times; 25 calls give the breaker's minimum throughput of 100.
        var client = CreateClient();
        _handler.RespondTo("/items", ServiceUnavailable);

        for (var i = 0; i < 25; i++)
        {
            using var failed = await client.GetAsync("items");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        }

        await Assert.ThrowsAsync<BrokenCircuitException>(() => client.GetAsync("items"));
    }

    [Fact]
    public async Task OneFailingSite_DoesNotOpenTheBreakerForAnotherSite()
    {
        // Jira and Azure DevOps send every customer's site through the same named client.
        var client = CreateClient();
        _handler.RespondTo("/down/items", ServiceUnavailable);
        _handler.RespondTo("/up/items", HttpStatusCode.OK);

        for (var i = 0; i < 25; i++)
        {
            using var failed = await client.GetAsync("https://down.example.test/down/items");
        }

        await Assert.ThrowsAsync<BrokenCircuitException>(
            () => client.GetAsync("https://down.example.test/down/items"));

        using var response = await client.GetAsync("https://up.example.test/up/items");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TooManyRequests_CountAsSuccesses_WhenMixedWithServerErrors()
    {
        // Polly has no neutral outcome, so 60 rate-limited calls are 60 successes: the breaker opens
        // only once server errors reach half of what it saw, after 60 failed attempts (15 calls).
        var client = CreateClient();
        _handler.RespondTo("/limited", () => TooManyRequests(retryAfterSeconds: 60));
        _handler.RespondTo("/failing", ServiceUnavailable);

        for (var i = 0; i < 60; i++)
        {
            using var limited = await client.GetAsync("limited");
        }

        var callsBeforeOpen = 0;
        while (callsBeforeOpen < 30)
        {
            try
            {
                using var failed = await client.GetAsync("failing");
                callsBeforeOpen++;
            }
            catch (BrokenCircuitException)
            {
                break;
            }
        }

        Assert.Equal(15, callsBeforeOpen);
    }

    /// <summary>A 503 that asks for no pause, so the retries do not slow the test down.</summary>
    private static HttpResponseMessage ServiceUnavailable()
    {
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{}") };
        response.Headers.Add("Retry-After", "0");
        return response;
    }

    [Fact]
    public async Task ConnectTimeout_IsStillRetried()
    {
        // HttpClient reports a connect timeout as a cancellation wrapping a TimeoutException.
        _handler.EnqueueException(new TaskCanceledException("connect timed out", new TimeoutException()));
        _handler.EnqueueResponse(HttpStatusCode.OK);

        using var response = await CreateClient().GetAsync("items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, _handler.SentRequests.Count);
    }
}
