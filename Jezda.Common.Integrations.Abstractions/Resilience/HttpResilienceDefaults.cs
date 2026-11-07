using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Jezda.Common.Integrations.Abstractions.Resilience;

public static class HttpResilienceDefaults
{
    /// <summary>
    /// Adds the standard resilience handler: up to 3 retries of transient failures, a circuit breaker,
    /// a 10 s timeout per attempt and 30 s in total.
    /// </summary>
    /// <remarks>
    /// A 429 is not retried, so the caller gets it at once, with its <c>Retry-After</c>. Retrying would
    /// wait out a <c>Retry-After</c> longer than the total timeout and end as a timeout instead of a
    /// 429, or keep calling a provider that asked for a pause, which GitHub warns can get an
    /// integration banned.
    /// </remarks>
    public static IHttpClientBuilder AddIntegrationResilience(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
            options.Retry.ShouldHandle = args => ValueTask.FromResult(
                HttpClientResiliencePredicates.IsTransient(args.Outcome)
                && args.Outcome.Result?.StatusCode != HttpStatusCode.TooManyRequests);

            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);

            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        });

        return builder;
    }
}
