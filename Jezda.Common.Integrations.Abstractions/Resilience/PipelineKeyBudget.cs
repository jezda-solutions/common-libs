using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jezda.Common.Integrations.Abstractions.Resilience;

/// <summary>
/// Bounds the pipelines one client keeps. Polly's registry keeps a pipeline for every key it is
/// given, for the life of the process, and tags its metrics with the key, so a key taken straight
/// from the request grows memory and metric series with every base URL a user types and every
/// token ever sent.
/// </summary>
/// <remarks>
/// A key gets a pipeline of its own while the budget lasts: at most <see cref="MaxKeys"/> in all, of
/// which at most <see cref="MaxUnprovenKeys"/> have not had a successful answer yet. Every other
/// request shares <see cref="OverflowKey"/>, a name reserved for that. A key whose request succeeds
/// through the shared pipeline is proven and gets its own on its next request, so a working site
/// keeps its own breaker even after mistyped or probed addresses have used up the unproven part.
/// Admitted keys are never dropped, because the registry cannot drop their pipelines. With
/// per-token keys every distinct token takes a place until the process restarts, which is fine for
/// long-lived tokens; rotating ones would need idle keys evicted (issue #230).
/// <para />
/// Each limit logs one warning per client the first time it sends a request to the shared pipeline,
/// so a filled budget can be told apart from an ordinary outage.
/// </remarks>
internal sealed class PipelineKeyBudget(string clientName, Func<HttpRequestMessage, string> pipelineKey)
{
    /// <summary>The most keys with a pipeline of their own, per client.</summary>
    public const int MaxKeys = 1000;

    /// <summary>The most of those that have not had a successful answer yet.</summary>
    public const int MaxUnprovenKeys = 100;

    /// <summary>The key every request beyond the budget shares. Holds no address or token.</summary>
    public const string OverflowKey = "overflow";

    /// <summary>
    /// The key <see cref="Select"/> computed, carried on the request to <see cref="RecordSuccess"/>:
    /// a redirect rewrites the URI and drops the <c>Authorization</c> header, so computing it again
    /// there would prove the wrong key. Per instance, so a second budget on the same request cannot
    /// overwrite it.
    /// </summary>
    private readonly HttpRequestOptionsKey<string> _requestKey = new($"Jezda.Integrations.PipelineKey.{Guid.NewGuid():N}");

    // Admitted keys, with whether they have had a successful answer yet.
    private readonly ConcurrentDictionary<string, bool> _admitted = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _proven = new(StringComparer.Ordinal);
    private readonly object _admitLock = new();

    // Counted apart from the dictionaries: ConcurrentDictionary.Count takes every lock it has.
    private int _admittedCount;
    private int _provenCount; // changed and read under the admission lock only
    private int _unprovenAdmitted;
    private int _warnedFull;
    private int _warnedUnprovenFull;

    /// <summary>Where the warnings go; set once the client's services are known.</summary>
    public ILogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>The pipeline key a request runs under.</summary>
    /// <remarks>
    /// The resilience library calls the selector once with a placeholder request each time it builds
    /// the handler, to check it. That placeholder always gives the same key and never answers, so it
    /// takes one unproven place, never more.
    /// </remarks>
    public string Select(HttpRequestMessage request)
    {
        var key = pipelineKey(request);
        request.Options.Set(_requestKey, key);

        if (_admitted.ContainsKey(key))
        {
            return key;
        }

        // Once the budget is full, overflow traffic does not queue on the lock.
        if (Volatile.Read(ref _admittedCount) >= MaxKeys)
        {
            WarnOnce(ref _warnedFull, $"all {MaxKeys} pipelines are taken");
            return OverflowKey;
        }

        lock (_admitLock)
        {
            if (_admitted.ContainsKey(key))
            {
                return key;
            }

            if (_admittedCount >= MaxKeys)
            {
                WarnOnce(ref _warnedFull, $"all {MaxKeys} pipelines are taken");
                return OverflowKey;
            }

            var proven = _proven.TryRemove(key, out _);

            if (proven)
            {
                _provenCount--;
            }
            else if (Volatile.Read(ref _unprovenAdmitted) >= MaxUnprovenKeys)
            {
                WarnOnce(ref _warnedUnprovenFull, $"the {MaxUnprovenKeys} pipelines for addresses that never answered successfully are taken");
                return OverflowKey;
            }
            else
            {
                Interlocked.Increment(ref _unprovenAdmitted);
            }

            _admitted[key] = proven;
            Volatile.Write(ref _admittedCount, _admittedCount + 1);

            return key;
        }
    }

    private void WarnOnce(ref int warned, string reason)
    {
        if (Interlocked.Exchange(ref warned, 1) == 1)
        {
            return;
        }

        Logger.LogWarning(
            "HTTP client {ClientName}: {Reason}, so new addresses or tokens now share one resilience pipeline and circuit breaker "
            + "until they answer successfully. Many distinct base URLs or tokens were used, for example mistyped or probed ones.",
            clientName,
            reason);
    }

    /// <summary>
    /// Records a successful answer to a request <see cref="Select"/> saw. An admitted key that had
    /// none yet gives its unproven place back, so working sites do not use up the places meant for
    /// addresses that never answered. A key not admitted is remembered only while there is room, so
    /// the record stays within the budget too.
    /// </summary>
    public void RecordSuccess(HttpRequestMessage request)
    {
        if (!request.Options.TryGetValue(_requestKey, out var key))
        {
            return;
        }

        if (_admitted.TryGetValue(key, out var proven))
        {
            if (!proven && _admitted.TryUpdate(key, true, false))
            {
                Interlocked.Decrement(ref _unprovenAdmitted);
            }

            return;
        }

        // Under the admission lock: otherwise Select could admit the key between the check above and
        // the record, leaving it both admitted and proven for good, or concurrent records could pass
        // the budget check together. Only successes on the shared pipeline come here.
        lock (_admitLock)
        {
            if (_admitted.ContainsKey(key) || _admittedCount + _provenCount >= MaxKeys)
            {
                return;
            }

            if (_proven.TryAdd(key, 0))
            {
                _provenCount++;
            }
        }
    }
}

/// <summary>
/// Sits inside the resilience handler and tells <see cref="PipelineKeyBudget"/> which keys got a
/// successful answer.
/// </summary>
internal sealed class PipelineKeyBudgetHandler(PipelineKeyBudget budget) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            budget.RecordSuccess(request);
        }

        return response;
    }
}
