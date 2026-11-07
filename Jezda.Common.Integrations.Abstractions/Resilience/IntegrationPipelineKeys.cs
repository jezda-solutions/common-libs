using System.Security.Cryptography;
using System.Text;

namespace Jezda.Common.Integrations.Abstractions.Resilience;

/// <summary>
/// Pipeline keys for <see cref="HttpResilienceDefaults.AddIntegrationResilience(Microsoft.Extensions.DependencyInjection.IHttpClientBuilder, Func{HttpRequestMessage, string})"/>.
/// Polly writes the key into logs and metrics, so none of them holds a secret in readable form.
/// </summary>
public static class IntegrationPipelineKeys
{
    /// <summary>
    /// The request's address: scheme, host and port. Built from components, not
    /// <see cref="Uri.GetLeftPart(UriPartial)"/>, which keeps a <c>user:pass@</c> typed into a base
    /// URL.
    /// </summary>
    /// <exception cref="InvalidOperationException">The request has no absolute URI.</exception>
    public static string Address(HttpRequestMessage request) =>
        AbsoluteUri(request).GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);

    /// <summary>
    /// The request's address plus a hash of its <c>Authorization</c> header, so each token on an
    /// address everyone shares gets its own circuit breaker. The address alone when the request
    /// carries no <c>Authorization</c> header.
    /// </summary>
    /// <remarks>
    /// The first 16 hex characters of the header's SHA-256: enough to keep tokens apart, never the
    /// token itself. Hashing a header of under a hundred bytes per attempt costs far less than the
    /// request it guards.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The request has no absolute URI.</exception>
    public static string AddressAndAuthorization(HttpRequestMessage request)
    {
        var address = Address(request);
        var authorization = request.Headers.Authorization?.ToString();

        if (string.IsNullOrEmpty(authorization))
        {
            return address;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(authorization));

        return $"{address}#{Convert.ToHexStringLower(hash, 0, 8)}";
    }

    private static Uri AbsoluteUri(HttpRequestMessage request) =>
        request.RequestUri is { IsAbsoluteUri: true } uri
            ? uri
            : throw new InvalidOperationException("The request has no absolute URI, so it has no resilience pipeline key.");
}
