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
    /// Each address (<see cref="IntegrationPipelineKeys.Address"/>: scheme, host and port) gets its own
    /// pipeline and so its own circuit breaker. Jira sends every customer's site through the same
    /// named client, so one site that is down or mistyped would otherwise open the breaker for all of
    /// them, and they would get Polly's <c>BrokenCircuitException</c> instead of an answer. A provider
    /// whose callers share one address passes its own key to the other overload: Azure DevOps adds the
    /// organisation, GitHub a hash of the token.
    /// <para />
    /// Everything in the pipeline is per key, the concurrency limit (1000) included. A client keeps
    /// at most 1000 pipelines, at most 100 of them for keys that had no successful answer yet, for
    /// the life of the process; requests beyond that share one pipeline. A key that once answered
    /// successfully gets its own as long as room is left, so a base URL a user mistyped, or probed
    /// on purpose, cannot take a working site's breaker away.
    /// <para />
    /// Tokens on the same key still share a breaker, so a 429 does not count towards it: one
    /// token's rate limit would otherwise open it for every other token. Polly has no neutral outcome,
    /// so the breaker records a 429 as a success: the provider did answer, which is what the breaker
    /// measures.
    /// </remarks>
    public static IHttpClientBuilder AddIntegrationResilience(this IHttpClientBuilder builder) =>
        builder.AddIntegrationResilience(IntegrationPipelineKeys.Address);

    /// <summary>
    /// Adds the same resilience handler as <see cref="AddIntegrationResilience(IHttpClientBuilder)"/>,
    /// with one pipeline, and so one circuit breaker, per key that <paramref name="pipelineKey"/>
    /// gives a request.
    /// </summary>
    /// <param name="builder">The client to add the handler to.</param>
    /// <param name="pipelineKey">
    /// The key of a request's pipeline, called once per request, before its retries. Polly writes it into logs and
    /// metrics, so it must never hold a token, password or other secret in readable form; start from
    /// <see cref="IntegrationPipelineKeys.Address"/>, which drops credentials typed into a URL.
    /// </param>
    /// <remarks>
    /// For a provider whose callers do not all fail together: every organisation on one host, or
    /// every token on one address (<see cref="IntegrationPipelineKeys.AddressAndAuthorization"/>).
    /// The number of pipelines is bounded as for the default key.
    /// </remarks>
    public static IHttpClientBuilder AddIntegrationResilience(
        this IHttpClientBuilder builder,
        Func<HttpRequestMessage, string> pipelineKey)
    {
        ArgumentNullException.ThrowIfNull(pipelineKey);

        var budget = new PipelineKeyBudget(pipelineKey);

        builder.AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
            options.Retry.ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Result is { StatusCode: HttpStatusCode.TooManyRequests } response
                    ? AsksForAShortPause(response)
                    : HttpClientResiliencePredicates.IsTransient(args.Outcome)
                      || IsConnectTimeout(args.Outcome.Exception, args.Context.CancellationToken));

            // With the default key the breaker is shared by every caller of the same address, whatever
            // their token, so it counts only what says the provider itself is failing. A 429 is one
            // token's rate limit: counted, it would open the breaker for every other token there.
            options.CircuitBreaker.ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Result is not { StatusCode: HttpStatusCode.TooManyRequests }
                && (HttpClientResiliencePredicates.IsTransient(args.Outcome)
                    || IsConnectTimeout(args.Outcome.Exception, args.Context.CancellationToken)));
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);

            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        }).SelectPipelineBy(_ => budget.Select);

        // Inside the resilience handler, so it sees each attempt's answer.
        builder.AddHttpMessageHandler(() => new PipelineKeyBudgetHandler(budget));

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
