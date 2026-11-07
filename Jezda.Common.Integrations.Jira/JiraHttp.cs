using System.Net;

namespace Jezda.Common.Integrations.Jira;

/// <summary>
/// HTTP behaviour both Jira clients share: the typed <see cref="IJiraClient"/> and the named client
/// behind <c>JiraTaskProvider</c>.
/// </summary>
internal static class JiraHttp
{
    /// <summary>
    /// Throws for a response that is not a Jira API answer: a redirect, or a success without JSON.
    /// Any other response, failed or not, is left to the caller.
    /// </summary>
    /// <remarks>
    /// Neither says anything about the token, so neither may read as a verdict on it. A redirect means
    /// the base URL answers somewhere else, or single sign-on stands in front of Jira. A success
    /// without JSON means a login page, a website or a proxy answered in Jira's place, and those answer
    /// 200 whatever the token is.
    /// </remarks>
    /// <exception cref="HttpRequestException">Carries the response's own status.</exception>
    public static void ThrowIfNotApiResponse(HttpResponseMessage response)
    {
        if ((int)response.StatusCode is >= 300 and < 400)
        {
            throw new HttpRequestException(
                $"Jira redirected {Describe(response.RequestMessage?.RequestUri) ?? "the request"} to {Describe(RedirectTarget(response)) ?? "(no location)"} "
                + $"with {(int)response.StatusCode}, so the token was not checked. "
                + "The base URL is likely out of date, for example after the site was renamed, or single sign-on stands in front of Jira.",
                inner: null,
                statusCode: response.StatusCode);
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;

        if (response.IsSuccessStatusCode && mediaType?.EndsWith("json", StringComparison.OrdinalIgnoreCase) != true)
        {
            throw new HttpRequestException(
                $"The server answered {(int)response.StatusCode} with {mediaType ?? "no content type"} instead of JSON, so the token was not checked. "
                + "The base URL likely points at a login page or a website, or a proxy answered in place of Jira.",
                inner: null,
                statusCode: response.StatusCode);
        }
    }

    private static Uri? RedirectTarget(HttpResponseMessage response)
    {
        var location = response.Headers.Location;

        return location is { IsAbsoluteUri: false } && response.RequestMessage?.RequestUri is { } requestUri
            ? new Uri(requestUri, location)
            : location;
    }

    // Host and path only. Credentials never go in a URL, but a person can type a username and password
    // into a base URL by mistake, and the exception would carry them into logs. A sign-in redirect's
    // query is an opaque context that only makes the message harder to read.
    private static string? Describe(Uri? uri) => uri switch
    {
        null => null,
        { IsAbsoluteUri: true } => uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped),
        _ => uri.OriginalString.Split('?', '#')[0]
    };
}
