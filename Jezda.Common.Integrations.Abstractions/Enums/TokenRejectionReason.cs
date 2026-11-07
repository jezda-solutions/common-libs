namespace Jezda.Common.Integrations.Abstractions.Enums;

/// <summary>
/// Why a provider did not accept a token, so a caller can tell the user what to change.
/// </summary>
public enum TokenRejectionReason
{
    /// <summary>
    /// The provider does not know the token: wrong, revoked or expired. A new token is needed.
    /// </summary>
    InvalidToken = 1,

    /// <summary>
    /// The token has a character that cannot go in an HTTP header, such as a pasted smart quote or
    /// line break, so it was not sent. Pasting it again without the stray character fixes it.
    /// </summary>
    NotSendable = 2,

    /// <summary>
    /// The provider knows the token but it lacks a permission the integration needs: for Azure DevOps,
    /// Work Items (Read); for Monday.com, <c>missingRequiredPermissions</c>. A token with that scope fixes it; a new token with the same scopes does not.
    /// </summary>
    MissingScope = 3,

    /// <summary>
    /// The provider refused access without saying why: a missing scope, Conditional Access, an IP
    /// allow list or a firewall in front of it. The token itself may be valid.
    /// </summary>
    AccessDenied = 4,

    /// <summary>
    /// The base URL answered with a sign-in page instead of the API: the token was not accepted, or a
    /// gateway in front of the provider asks for its own sign-in.
    /// <see cref="Models.ConnectionValidationResult.Detail"/> names where the sign-in page is.
    /// </summary>
    SignInPage = 5
}
