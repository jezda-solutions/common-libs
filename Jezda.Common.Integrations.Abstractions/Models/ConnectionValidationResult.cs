using Jezda.Common.Integrations.Abstractions.Enums;

namespace Jezda.Common.Integrations.Abstractions.Models;

/// <summary>
/// The answer of <see cref="IExternalTaskProvider.ValidateConnectionAsync"/>: whether the provider
/// accepted the token and, when it did not, why.
/// </summary>
public sealed record ConnectionValidationResult
{
    private ConnectionValidationResult(TokenRejectionReason? reason, string? detail)
    {
        Reason = reason;
        Detail = detail;
    }

    /// <summary>The token is accepted.</summary>
    public static ConnectionValidationResult Accepted { get; } = new(null, null);

    /// <summary><see langword="true"/> when the provider accepted the token.</summary>
    public bool IsValid => Reason is null;

    /// <summary>Why the token was not accepted; <see langword="null"/> when it was.</summary>
    public TokenRejectionReason? Reason { get; }

    /// <summary>
    /// What the provider answered, for logs and support: the status and the path it came on, or
    /// where a sign-in page is. English, not meant to be shown to users as it is; map
    /// <see cref="Reason"/> to a message instead. It never holds the token or the response body.
    /// </summary>
    public string? Detail { get; }

    /// <summary>A token the provider did not accept, with the reason and what it answered.</summary>
    public static ConnectionValidationResult Rejected(TokenRejectionReason reason, string? detail = null) =>
        new(reason, detail);
}
