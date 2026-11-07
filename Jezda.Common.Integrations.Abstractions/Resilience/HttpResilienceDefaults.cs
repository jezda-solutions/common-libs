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
    /// A 429 is retried only when its <c>Retry-After</c> asks for at most
    /// <see cref="MaxRetryAfterToRetry"/>, waiting what it asks. A longer pause, or a 429 without
    /// <c>Retry-After</c>, reaches the caller at once: waiting it out would pass the total timeout and
    /// end as a timeout instead of a 429, and calling again without being told when keeps calling a
    /// provider that asked for a pause, which GitHub warns can get an integration banned.
    /// </remarks>
    public static IHttpClientBuilder AddIntegrationResilience(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
            options.Retry.ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Result is { StatusCode: HttpStatusCode.TooManyRequests } response
                    ? AsksForAShortPause(response)
                    : HttpClientResiliencePredicates.IsTransient(args.Outcome));

            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);

            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        });

        return builder;
    }

    /// <summary>
    /// The longest <c>Retry-After</c> a 429 is retried for. Three such waits fit within the 30 s total.
    /// </summary>
    private static readonly TimeSpan MaxRetryAfterToRetry = TimeSpan.FromSeconds(5);

    private static bool AsksForAShortPause(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var wait = retryAfter?.Delta
            ?? (retryAfter?.Date is { } date ? date - (response.Headers.Date ?? DateTimeOffset.UtcNow) : null);

        return wait is { } pause && pause <= MaxRetryAfterToRetry;
    }
}
