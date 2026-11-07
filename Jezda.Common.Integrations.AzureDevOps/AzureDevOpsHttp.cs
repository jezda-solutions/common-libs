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
    /// page.
    /// </summary>
    public const string FedAuthRedirectHeader = "X-TFS-FedAuthRedirect";

    /// <inheritdoc cref="FedAuthRedirectHeader"/>
    public const string FedAuthRedirectSuppress = "Suppress";

    /// <summary>
    /// The organisation a client talks to, for log messages. The base URL comes from the user, so this
    /// leaves out any credentials written into it.
    /// </summary>
    public static string? OrganisationForLog(HttpClient client) => Describe(client.BaseAddress);

    /// <summary>
    /// Whether a response is a sign-in page, or a redirect to one, instead of an API answer.
    /// </summary>
    /// <remarks>
    /// <see cref="FedAuthRedirectHeader"/> is the first defence, but a proxy that drops the header,
    /// or a server that ignores it, brings the sign-in flow back. Without the header Azure DevOps
    /// answers a rejected token with 302 to <c>/_signin</c> on its identity host, which is the first
    /// response seen here because redirects are not followed. The page behind that redirect is served
    /// as 203 with HTML, so 203 with anything but JSON also means the token was not accepted. 203 with
    /// JSON stays an answer: a transforming proxy can return a valid response that way.
    /// <para />
    /// A body without JSON on any other success status is not a sign-in page: see
    /// <see cref="ThrowIfNotApiResponse"/>.
    /// </remarks>
    public static bool IsSignInPage(HttpResponseMessage response)
    {
        if (IsRedirect(response))
        {
            return response.Headers.Location?.OriginalString.Split('?', '#')[0]
                .EndsWith("/_signin", StringComparison.OrdinalIgnoreCase) == true;
        }

        return response.StatusCode == HttpStatusCode.NonAuthoritativeInformation && !IsJson(response);
    }

    /// <summary>
    /// Throws for a response that is not an Azure DevOps API answer: a sign-in page, a redirect, or
    /// a success that is not JSON, 204 included. Every Azure DevOps call here reads a JSON answer, so
    /// an empty one is not "nothing found" but something other than Azure DevOps answering; a call
    /// added later that legitimately answers 204, such as a delete, has to check before this. Any
    /// other response, failed or not, is left to the caller.
    /// </summary>
    /// <exception cref="HttpRequestException">
    /// <see cref="HttpStatusCode.Unauthorized"/> for a sign-in page or a redirect to one, so callers
    /// that treat 401 as an expired token keep working. The response's own status for any other
    /// redirect or a success without JSON, neither of which says anything about the token: the base URL answers
    /// somewhere else, or something other than Azure DevOps answered in its place.
    /// </exception>
    public static void ThrowIfNotApiResponse(HttpResponseMessage response)
    {
        if (IsSignInPage(response))
        {
            var answer = IsRedirect(response)
                ? $"redirected to the sign-in page at {Describe(RedirectTarget(response))} with {(int)response.StatusCode}"
                : $"answered {(int)response.StatusCode} with a sign-in page instead of JSON";

            throw new HttpRequestException(
                $"Azure DevOps {answer}: the token was not accepted.",
                inner: null,
                statusCode: HttpStatusCode.Unauthorized);
        }

        if (IsRedirect(response))
        {
            throw new HttpRequestException(
                $"Azure DevOps redirected {Describe(response.RequestMessage?.RequestUri) ?? "the request"} to {Describe(RedirectTarget(response)) ?? "(no location)"} "
                + $"with {(int)response.StatusCode}. The base URL is likely out of date, for example after the organisation was renamed.",
                inner: null,
                statusCode: response.StatusCode);
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;

        if (response.IsSuccessStatusCode && !IsJson(response))
        {
            throw new HttpRequestException(
                $"The server answered {(int)response.StatusCode} with {mediaType ?? "no content type"} instead of JSON, so the token was not checked. "
                + "The base URL likely points at a website, or a proxy or captive portal answered in place of Azure DevOps.",
                inner: null,
                statusCode: response.StatusCode);
        }
    }

    private static bool IsRedirect(HttpResponseMessage response) => (int)response.StatusCode is >= 300 and < 400;

    private static bool IsJson(HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType?.EndsWith("json", StringComparison.OrdinalIgnoreCase) == true;

    private static Uri? RedirectTarget(HttpResponseMessage response)
    {
        var location = response.Headers.Location;

        return location is { IsAbsoluteUri: false } && response.RequestMessage?.RequestUri is { } requestUri
            ? new Uri(requestUri, location)
            : location;
    }

    // Host and path only: enough to tell Azure DevOps from a gateway with its own /_signin. A base URL
    // can carry a password, which the exception would carry into logs, and a sign-in redirect's query
    // holds the reply URL and an opaque context, which only make the message harder to read.
    private static string? Describe(Uri? uri) => uri switch
    {
        null => null,
        { IsAbsoluteUri: true } => uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped),
        _ => uri.OriginalString.Split('?', '#')[0]
    };
}
