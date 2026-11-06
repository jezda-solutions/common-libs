using System.Net;

namespace Jezda.Common.Integrations.AzureDevOps;

/// <summary>
/// HTTP behaviour both Azure DevOps clients share: the typed <see cref="IAzureDevOpsClient"/> and
/// the named client behind <c>AzureDevOpsTaskProvider</c>.
/// </summary>
internal static class AzureDevOpsHttp
{
    /// <summary>
    /// Asks Azure DevOps to answer a rejected token with 401 instead of redirecting it to a sign-in
    /// page. Without it, the redirect chain ends as 203 with HTML, which
    /// <see cref="HttpResponseMessage.IsSuccessStatusCode"/> accepts.
    /// </summary>
    public const string FedAuthRedirectHeader = "X-TFS-FedAuthRedirect";

    /// <inheritdoc cref="FedAuthRedirectHeader"/>
    public const string FedAuthRedirectSuppress = "Suppress";

    /// <summary>
    /// Whether a success status carries a sign-in page instead of an API answer.
    /// </summary>
    /// <remarks>
    /// <see cref="FedAuthRedirectHeader"/> is the first defence, but a proxy that drops the header,
    /// or a server that ignores it, brings the sign-in page back. Azure DevOps answers its REST API
    /// with JSON, so 203 or an HTML body on a success status means the token was not accepted.
    /// </remarks>
    public static bool IsSignInPage(HttpResponseMessage response) =>
        response.IsSuccessStatusCode
        && (response.StatusCode == HttpStatusCode.NonAuthoritativeInformation
            || string.Equals(response.Content.Headers.ContentType?.MediaType, "text/html", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Throws for a response that is not an Azure DevOps API answer: a sign-in page, or a redirect.
    /// Any other response, failed or not, is left to the caller.
    /// </summary>
    /// <exception cref="HttpRequestException">
    /// <see cref="HttpStatusCode.Unauthorized"/> for a sign-in page, so callers that treat 401 as an
    /// expired token keep working. The redirect's own 3xx status for a redirect, which is not a token
    /// problem: the base URL answers somewhere else.
    /// </exception>
    public static void ThrowIfNotApiResponse(HttpResponseMessage response)
    {
        if (IsSignInPage(response))
        {
            throw new HttpRequestException(
                $"Azure DevOps answered {(int)response.StatusCode} with a sign-in page instead of JSON: the token was not accepted.",
                inner: null,
                statusCode: HttpStatusCode.Unauthorized);
        }

        if ((int)response.StatusCode is >= 300 and < 400)
        {
            var location = response.Headers.Location;
            if (location is not null && !location.IsAbsoluteUri && response.RequestMessage?.RequestUri is { } requestUri)
            {
                location = new Uri(requestUri, location);
            }

            throw new HttpRequestException(
                $"Azure DevOps redirected {response.RequestMessage?.RequestUri} to {location?.ToString() ?? "(no location)"} "
                + $"with {(int)response.StatusCode}. The base URL is likely out of date, for example after the organisation was renamed.",
                inner: null,
                statusCode: response.StatusCode);
        }
    }

    /// <summary>
    /// Stops the primary handler from following redirects.
    /// </summary>
    /// <remarks>
    /// .NET drops the <c>Authorization</c> header when it follows a redirect, so a base URL that
    /// answers 302 (an organisation renamed, a stored URL in an old form) would answer the next hop
    /// with 401 even for a valid PAT, and read as an expired token. Not following it surfaces the
    /// redirect itself, through <see cref="ThrowIfNotApiResponse"/>.
    /// </remarks>
    public static void DisableAutoRedirect(HttpMessageHandler primaryHandler)
    {
        switch (primaryHandler)
        {
            case SocketsHttpHandler sockets:
                sockets.AllowAutoRedirect = false;
                break;
            case HttpClientHandler client:
                client.AllowAutoRedirect = false;
                break;
        }
    }
}
