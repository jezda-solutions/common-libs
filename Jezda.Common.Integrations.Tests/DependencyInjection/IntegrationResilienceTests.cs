using System.Net;
using Jezda.Common.Integrations.Abstractions.Resilience;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task TooManyRequests_IsNotRetried()
    {
        // A retry would wait out Retry-After past the total timeout and turn the 429 into a timeout.
        _handler.EnqueueResponse(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{}") };
            response.Headers.Add("Retry-After", "60");
            return response;
        });
        _handler.EnqueueResponse(HttpStatusCode.OK);

        using var response = await CreateClient().GetAsync("items");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.RetryAfter?.Delta);
        Assert.Single(_handler.SentRequests);
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
}
