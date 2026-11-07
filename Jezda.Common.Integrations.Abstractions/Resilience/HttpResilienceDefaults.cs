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
    /// Each address gets its own pipeline and so its own circuit breaker: scheme, host and port, plus
    /// the organisation on <c>dev.azure.com</c>, where every organisation shares one host. Jira and
    /// Azure DevOps send every customer's site or organisation through the same named client, so one
    /// site that is down or mistyped would otherwise open the breaker for all of them, and they would
    /// get Polly's <c>BrokenCircuitException</c> instead of an answer. The key never holds a username
    /// or password typed into a base URL, because Polly writes it into logs and metrics.
    /// <para />
    /// Everything in the pipeline is per address, the concurrency limit (1000) included. A pipeline is
    /// kept for every address the client has called, for the life of the process; base URLs a user
    /// typed count too, not only the configured sites.
    /// <para />
    /// Tokens on the same address still share a breaker, so a 429 does not count towards it: one
    /// token's rate limit would otherwise open it for every other token. Polly has no neutral outcome,
    /// so the breaker records a 429 as a success: the provider did answer, which is what the breaker
    /// measures.
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

            // The breaker is shared by every caller of the same address, whatever their token, so it
            // counts only what says the provider itself is failing. A 429 is one token's rate limit:
            // counted, it would open the breaker for every other token on the same address.
            options.CircuitBreaker.ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Result is not { StatusCode: HttpStatusCode.TooManyRequests }
                && (HttpClientResiliencePredicates.IsTransient(args.Outcome)
                    || IsConnectTimeout(args.Outcome.Exception, args.Context.CancellationToken)));
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);

            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        }).SelectPipelineBy(_ => PipelineKey);

        return builder;
    }

    /// <summary>
    /// The address a request's breaker belongs to: scheme, host and port, plus the first path
    /// segment on <c>dev.azure.com</c>. Built from components, not
    /// <see cref="Uri.GetLeftPart(UriPartial)"/>, which keeps <c>user:pass@</c>.
    /// </summary>
    private static string PipelineKey(HttpRequestMessage request)
    {
        if (request.RequestUri is not { IsAbsoluteUri: true } uri)
        {
            return string.Empty;
        }

        var address = uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);

        if (!uri.Host.Equals(SharedAzureDevOpsHost, StringComparison.OrdinalIgnoreCase))
        {
            return address;
        }

        var organisation = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        return organisation is null ? address : $"{address}/{organisation.ToLowerInvariant()}";
    }

    private const string SharedAzureDevOpsHost = "dev.azure.com";

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
