namespace Jezda.Common.Integrations.Abstractions.Resilience;

/// <summary>
/// Whose requests share one resilience pipeline key, which decides what a half-open circuit
/// breaker's single probe may conclude.
/// </summary>
public enum PipelineKeyScope
{
    /// <summary>
    /// Several tokens share the key, such as every caller of one Jira site
    /// (<see cref="IntegrationPipelineKeys.Address"/>). A probe answered 429, 401 or 403 speaks only
    /// for its own token, so it keeps the breaker open.
    /// </summary>
    SharedByTokens,

    /// <summary>
    /// The key belongs to one token (<see cref="IntegrationPipelineKeys.AddressAndAuthorization"/>).
    /// Any answer from the provider, a 429 included, shows it is up, so the probe may close the breaker
    /// and the caller gets the 429 with its retry time instead of <c>BrokenCircuitException</c>.
    /// </summary>
    PerToken
}
