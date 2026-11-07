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
    /// <para />
    /// The circuit breaker is shared by everyone using the same named client, so a 429 does not count
    /// towards it: one token's rate limit would otherwise open it for every other token, and they
    /// would get Polly's <c>BrokenCircuitException</c> instead of an answer. Polly has no neutral outcome, so the breaker
    /// records a 429 as a success: the provider did answer, which is what the breaker measures.
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
                    : HttpClientResiliencePredicates.IsTransient(args.Outcome)
                      || IsConnectTimeout(args.Outcome.Exception, args.Context.CancellationToken));

            // The breaker is shared by every caller of the named client, whatever their token, so it
            // counts only what says the provider itself is failing. A 429 is one token's rate limit:
            // counted, it would open the breaker for every other token on the same provider.
            options.CircuitBreaker.ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Result is not { StatusCode: HttpStatusCode.TooManyRequests }
                && (HttpClientResiliencePredicates.IsTransient(args.Outcome)
                    || IsConnectTimeout(args.Outcome.Exception, args.Context.CancellationToken)));
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);

            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        });

        return builder;
    }

    /// <summary>
    /// The longest <c>Retry-After</c> a 429 is retried for. Kept short so a retry usually ends before
    /// the 30 s total; slow attempts between the waits can still reach it.
    /// </summary>
    private static readonly TimeSpan MaxRetryAfterToRetry = TimeSpan.FromSeconds(5);

    private static bool AsksForAShortPause(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        var wait = retryAfter?.Delta
            ?? (retryAfter?.Date is { } date ? date - (response.Headers.Date ?? DateTimeOffset.UtcNow) : null);

        return wait is { } pause && pause <= MaxRetryAfterToRetry;
    }

    /// <summary>
    /// A connect timeout, which HttpClient reports as a cancellation wrapping a
    /// <see cref="TimeoutException"/> while the caller's token is not cancelled. The default
    /// predicate counts it as transient through an experimental overload, so it is checked here.
    /// </summary>
    private static bool IsConnectTimeout(Exception? exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException { InnerException: TimeoutException }
        && !cancellationToken.IsCancellationRequested;
}
