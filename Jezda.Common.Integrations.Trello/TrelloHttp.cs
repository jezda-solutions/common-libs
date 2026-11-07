using System.Security.Cryptography;
using System.Text;
using System.Web;
using Jezda.Common.Integrations.Abstractions.Resilience;

namespace Jezda.Common.Integrations.Trello;

/// <summary>HTTP behaviour of the named client behind <c>TrelloTaskProvider</c>.</summary>
internal static class TrelloHttp
{
    /// <summary>
    /// The resilience pipeline key of a request: its address plus a hash of the <c>token</c> query
    /// parameter, so each token on <c>api.trello.com</c> gets its own circuit breaker, as GitHub's
    /// do. Trello takes the token in the query string, not the <c>Authorization</c> header that
    /// <see cref="IntegrationPipelineKeys.AddressAndAuthorization"/> hashes. The first 16 hex
    /// characters of its SHA-256, never the token itself; the address alone without a token.
    /// </summary>
    public static string PipelineKey(HttpRequestMessage request)
    {
        var address = IntegrationPipelineKeys.Address(request);
        var token = HttpUtility.ParseQueryString(request.RequestUri!.Query)["token"];

        if (string.IsNullOrEmpty(token))
        {
            return address;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));

        return $"{address}#{Convert.ToHexStringLower(hash, 0, 8)}";
    }
}
