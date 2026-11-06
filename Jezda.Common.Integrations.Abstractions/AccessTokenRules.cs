namespace Jezda.Common.Integrations.Abstractions;

public static class AccessTokenRules
{
    /// <summary>
    /// Tells whether a token can be sent as it is in a request header.
    /// </summary>
    /// <remarks>
    /// A real token is printable ASCII, so anything else, such as a pasted smart quote or line break,
    /// cannot be a good one. Other characters mostly fail in the HTTP stack and read as an outage,
    /// non-ASCII ones after retries that count towards the circuit breaker every caller shares.
    /// </remarks>
    public static bool CanGoInHeader(string accessToken) =>
        !accessToken.AsSpan().ContainsAnyExceptInRange(' ', '~');
}
