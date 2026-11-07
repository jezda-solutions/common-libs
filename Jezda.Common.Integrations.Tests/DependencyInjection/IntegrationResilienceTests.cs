using System.Net;
using Jezda.Common.Integrations.Abstractions.Resilience;
using Jezda.Common.Integrations.AzureDevOps.Extensions;
using Jezda.Common.Integrations.AzureDevOps.Providers;
using Jezda.Common.Integrations.GitHub.Extensions;
using Jezda.Common.Integrations.GitHub.Providers;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Configuration;
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
    public async Task OneFailingAzureDevOpsOrganisation_DoesNotOpenTheBreakerForAnother()
    {
        // Every Azure DevOps Services organisation is on the same host, dev.azure.com.
        var client = CreateProviderClient(AzureDevOpsTaskProvider.HttpClientName);
        _handler.RespondTo("/orgA/items", ServiceUnavailable);
        _handler.RespondTo("/orgB/items", HttpStatusCode.OK);

        await OpenBreakerAsync(client, "https://dev.azure.com/orgA/items");

        using var response = await client.GetAsync("https://dev.azure.com/orgB/items");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OneFailingGitHubToken_DoesNotOpenTheBreakerForAnotherToken()
    {
        // Every GitHub caller is on api.github.com.
        var client = CreateProviderClient(GitHubTaskProvider.HttpClientName);
        _handler.RespondTo("/search/issues", ServiceUnavailable);

        await OpenBreakerAsync(client, "search/issues", token: "token-a");

        using var other = Request("search/issues", token: "token-b");
        using var response = await client.SendAsync(other);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task GitHubToken_FailingAlone_StillOpensItsOwnBreaker()
    {
        var client = CreateProviderClient(GitHubTaskProvider.HttpClientName);
        _handler.RespondTo("/search/issues", ServiceUnavailable);

        await OpenBreakerAsync(client, "search/issues", token: "token-a");

        using var again = Request("search/issues", token: "token-a");
        await Assert.ThrowsAsync<BrokenCircuitException>(() => client.SendAsync(again));
    }

    [Theory]
    // Credentials typed into a base URL stay out of the key: the same site, one breaker.
    [InlineData(null, "https://user:secret@down.example.test/down/items", "https://down.example.test/down/items")]
    // Azure DevOps reads organisation names case-insensitively and unescaped.
    [InlineData(AzureDevOpsTaskProvider.HttpClientName, "https://dev.azure.com/OrgA/items", "https://dev.azure.com/orga/items")]
    [InlineData(AzureDevOpsTaskProvider.HttpClientName, "https://dev.azure.com/Org%41/items", "https://dev.azure.com/orga/items")]
    public async Task SameAddress_WrittenDifferently_SharesOneBreaker(string? providerClient, string failingUrl, string sameAddressUrl)
    {
        var client = providerClient is null ? CreateClient() : CreateProviderClient(providerClient);
        _handler.RespondTo(new Uri(failingUrl).AbsolutePath, ServiceUnavailable);
        // A 503 too, not an answer: with a key of its own this URL would get it instead of the exception.
        _handler.RespondTo(new Uri(sameAddressUrl).AbsolutePath, ServiceUnavailable);

        await OpenBreakerAsync(client, failingUrl);

        await Assert.ThrowsAsync<BrokenCircuitException>(() => client.GetAsync(sameAddressUrl));
    }

    [Fact]
    public void AddressAndAuthorization_HoldsAHashOfTheToken_NeverTheToken()
    {
        using var request = Request("https://user:secret@api.github.com/search/issues", token: "ghp_secret-token");

        var key = IntegrationPipelineKeys.AddressAndAuthorization(request);

        Assert.StartsWith("https://api.github.com#", key);
        Assert.Equal("https://api.github.com#".Length + 16, key.Length);
        Assert.DoesNotContain("secret", key);
    }

    [Fact]
    public void AddressAndAuthorization_WithoutAuthorization_IsTheAddress()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/search/issues");

        Assert.Equal("https://api.github.com", IntegrationPipelineKeys.AddressAndAuthorization(request));
    }

    [Fact]
    public void Address_OfARelativeUri_Throws()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "items");

        Assert.Throws<InvalidOperationException>(() => IntegrationPipelineKeys.Address(request));
    }

    [Fact]
    public void AddIntegrationResilience_WithoutAKey_Throws()
    {
        var builder = new ServiceCollection().AddHttpClient(ClientName);

        Assert.Throws<ArgumentNullException>(() => builder.AddIntegrationResilience(pipelineKey: null!));
    }

    /// <summary>
    /// The named client a provider registers, with its own pipeline key, sending to the mock handler.
    /// </summary>
    private HttpClient CreateProviderClient(string clientName)
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddAzureDevOpsIntegration(configuration);
        services.AddGitHubIntegration(configuration);
        services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => _handler);

        return services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
    }

    /// <summary>Each call is tried four times; 25 calls give the breaker's minimum throughput of 100.</summary>
    private static async Task OpenBreakerAsync(HttpClient client, string url, string? token = null)
    {
        for (var i = 0; i < 25; i++)
        {
            using var request = Request(url, token);
            using var failed = await client.SendAsync(request);
        }

        using var last = Request(url, token);
        await Assert.ThrowsAsync<BrokenCircuitException>(() => client.SendAsync(last));
    }

    private static HttpRequestMessage Request(string url, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.RelativeOrAbsolute));

        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return request;
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
