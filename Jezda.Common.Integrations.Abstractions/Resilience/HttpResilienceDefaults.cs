using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;

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
    /// at most 1000 pipelines, at most 100 of them for keys that have had no successful answer yet, for
    /// the life of the process; requests beyond that share one pipeline. A key that once answered
    /// successfully gets its own as long as room is left, so a base URL a user mistyped, or probed
    /// on purpose, cannot take a working site's breaker away.
    /// <para />
    /// Tokens on the same key still share a breaker, so a 429 does not count towards it: one
    /// token's rate limit would otherwise open it for every other token. Polly has no neutral outcome,
    /// so the breaker records a 429 as a success: the provider did answer, which is what the breaker
    /// measures. The exception is the single probe of a half-open breaker: a 429, 401 or 403 there
    /// keeps the breaker open, since it speaks only for the probe's token, not for whether the
    /// provider recovered. A provider keyed per token says so with <see cref="PipelineKeyScope.PerToken"/>.
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
    /// <see cref="IntegrationPipelineKeys.Address"/>, which drops credentials typed into a URL. The
    /// key <c>overflow</c> is reserved for the pipeline requests beyond the budget share.
    /// </param>
    /// <remarks>
    /// For a provider whose callers do not all fail together: every organisation on one host, or
    /// every token on one address (<see cref="IntegrationPipelineKeys.AddressAndAuthorization"/>).
    /// The number of pipelines is bounded as for the default key.
    /// </remarks>
    public static IHttpClientBuilder AddIntegrationResilience(
        this IHttpClientBuilder builder,
        Func<HttpRequestMessage, string> pipelineKey) =>
        builder.AddIntegrationResilience(pipelineKey, PipelineKeyScope.SharedByTokens);

    /// <summary>
    /// Adds the same resilience handler as
    /// <see cref="AddIntegrationResilience(IHttpClientBuilder, Func{HttpRequestMessage, string})"/>,
    /// saying whether a key belongs to one token.
    /// </summary>
    /// <param name="builder">The client to add the handler to.</param>
    /// <param name="pipelineKey">The key of a request's pipeline; the same rules apply.</param>
    /// <param name="scope">
    /// <see cref="PipelineKeyScope.PerToken"/> when every key holds the token, as
    /// <see cref="IntegrationPipelineKeys.AddressAndAuthorization"/> does. It decides what the single
    /// probe of a half-open breaker may conclude: on a key several tokens share, a probe answered 429,
    /// 401 or 403 speaks only for its own token and keeps the breaker open; on a per-token key any
    /// answer shows the provider is up and may close it.
    /// </param>
    public static IHttpClientBuilder AddIntegrationResilience(
        this IHttpClientBuilder builder,
        Func<HttpRequestMessage, string> pipelineKey,
        PipelineKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(pipelineKey);
        var shared = scope == PipelineKeyScope.SharedByTokens;

        var budget = new PipelineKeyBudget(builder.Name, pipelineKey);

        builder.AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
            // On a shared key a probe's 429 has just reopened the breaker, so a retry would only meet
            // the open breaker and hand the caller BrokenCircuitException instead of the 429.
            options.Retry.ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Result is { StatusCode: HttpStatusCode.TooManyRequests } response
                    ? !(shared && IsProbe(args.Context)) && AsksForAShortPause(response)
                    : HttpClientResiliencePredicates.IsTransient(args.Outcome)
                      || IsConnectTimeout(args.Outcome.Exception, args.Context.CancellationToken));

            // The mark belongs to the attempt that was the probe. Left on, a later attempt of the same
            // execution, after another request's probe closed the breaker, would be judged as a probe.
            options.Retry.OnRetry = args =>
            {
                args.Context.Properties.Set(HalfOpenProbe, false);
                return default;
            };

            // With the default key the breaker is shared by every caller of the same address, whatever
            // their token, so it counts only what says the provider itself is failing. A 429 is one
            // token's rate limit: counted, it would open the breaker for every other token there.
            // Recorded as a success instead, it would close a half-open breaker whose single probe
            // happened to carry a rate-limited token, while the site is still failing; so a 429 to
            // that probe counts as a failure and the breaker stays open. Polly raises OnHalfOpened
            // with the probe's own context before the probe runs, so the mark is on that one
            // execution: a 429 to a request that started before the breaker opened stays neutral.
            //
            // The same holds for 401 and 403 on a shared key: a probe carrying a revoked or
            // under-scoped token would otherwise close the breaker for every other token. On a
            // per-token key the probe carries the key's own token, so any answer shows the provider
            // is up and may close it; the caller then gets the 429 with its retry time.
            options.CircuitBreaker.ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Result is { StatusCode: HttpStatusCode.TooManyRequests or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden }
                    ? shared && IsProbe(args.Context)
                    : HttpClientResiliencePredicates.IsTransient(args.Outcome)
                      || IsConnectTimeout(args.Outcome.Exception, args.Context.CancellationToken));
            options.CircuitBreaker.OnHalfOpened = args =>
            {
                args.Context.Properties.Set(HalfOpenProbe, true);
                return default;
            };
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);

            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        }).SelectPipelineBy(services =>
        {
            budget.Logger = services.GetService<ILoggerFactory>()?.CreateLogger(typeof(HttpResilienceDefaults))
                ?? budget.Logger;
            return budget.Select;
        });

        // Inside the resilience handler, so it sees each attempt's answer.
        builder.AddHttpMessageHandler(() => new PipelineKeyBudgetHandler(budget));

        return builder;
    }

    /// <summary>Set on the execution that carries a half-open breaker's probe.</summary>
    private static readonly ResiliencePropertyKey<bool> HalfOpenProbe = new("Jezda.Integrations.HalfOpenProbe");

    private static bool IsProbe(ResilienceContext context) =>
        context.Properties.TryGetValue(HalfOpenProbe, out var probe) && probe;

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
