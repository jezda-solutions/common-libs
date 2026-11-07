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

    /// <summary>
    /// Throws when a token cannot be sent as it is in a request header, before any request is made.
    /// </summary>
    /// <remarks>
    /// Such a token would otherwise fail inside the HTTP stack as an <see cref="HttpRequestException"/>
    /// without a status, which the resilience pipeline retries and counts towards the circuit breaker
    /// that every caller of the same provider shares.
    /// </remarks>
    /// <exception cref="ArgumentException">The token has a character outside printable ASCII.</exception>
    public static void ThrowIfCannotGoInHeader(string accessToken, string paramName = "accessToken")
    {
        if (!CanGoInHeader(accessToken))
        {
            throw new ArgumentException(
                "The access token has characters that cannot go in a request header, such as a pasted smart quote or line break.",
                paramName);
        }
    }
}
