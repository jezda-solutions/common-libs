using System.Collections.Concurrent;

namespace Jezda.Common.Integrations.Abstractions.Resilience;

/// <summary>
/// Bounds the pipelines one client keeps. Polly's registry keeps a pipeline for every key it is
/// given, for the life of the process, and tags its metrics with the key, so a key taken straight
/// from the request grows memory and metric series with every base URL a user types and every
/// token ever sent.
/// </summary>
/// <remarks>
/// A key gets a pipeline of its own while the budget lasts: at most <see cref="MaxKeys"/> in all, of
/// which at most <see cref="MaxUnprovenKeys"/> have never had a successful answer. Every other request
/// shares <see cref="OverflowKey"/>. A key whose request succeeds through the shared pipeline is
/// proven and gets its own on its next request, so a working site keeps its own breaker even after
/// mistyped or probed addresses have used up the unproven part. Admitted keys are never dropped,
/// because the registry cannot drop their pipelines.
/// </remarks>
internal sealed class PipelineKeyBudget(Func<HttpRequestMessage, string> pipelineKey)
{
    /// <summary>The most keys with a pipeline of their own, per client.</summary>
    public const int MaxKeys = 1000;

    /// <summary>The most of those that had no successful answer when they were admitted.</summary>
    public const int MaxUnprovenKeys = 100;

    /// <summary>The key every request beyond the budget shares. Holds no address or token.</summary>
    public const string OverflowKey = "overflow";

    private readonly ConcurrentDictionary<string, byte> _admitted = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _proven = new(StringComparer.Ordinal);
    private readonly object _admitLock = new();
    private int _unprovenAdmitted;

    /// <summary>The pipeline key a request runs under.</summary>
    /// <remarks>
    /// The resilience library calls the selector once with a placeholder request each time it builds
    /// the handler, to check it. That placeholder always gives the same key, so it takes one unproven
    /// place, never more.
    /// </remarks>
    public string Select(HttpRequestMessage request)
    {
        var key = pipelineKey(request);

        if (_admitted.ContainsKey(key))
        {
            return key;
        }

        lock (_admitLock)
        {
            if (_admitted.ContainsKey(key))
            {
                return key;
            }

            if (_admitted.Count >= MaxKeys)
            {
                return OverflowKey;
            }

            var proven = _proven.TryRemove(key, out _);

            if (!proven && _unprovenAdmitted >= MaxUnprovenKeys)
            {
                return OverflowKey;
            }

            if (!proven)
            {
                _unprovenAdmitted++;
            }

            _admitted[key] = 0;

            return key;
        }
    }

    /// <summary>
    /// Records a successful answer to a request. Only a key not yet admitted needs it, and only
    /// while there is room, so the record stays within the budget too.
    /// </summary>
    public void RecordSuccess(HttpRequestMessage request)
    {
        var key = pipelineKey(request);

        if (_admitted.ContainsKey(key) || _admitted.Count + _proven.Count >= MaxKeys)
        {
            return;
        }

        _proven.TryAdd(key, 0);
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
