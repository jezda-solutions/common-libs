namespace Jezda.Common.Integrations.Http;

/// <summary>
/// Turns a base URL from a user or from configuration into an <see cref="HttpClient.BaseAddress"/>.
/// </summary>
public static class IntegrationBaseAddress
{
    /// <summary>
    /// Keeps the scheme, host, port and path of <paramref name="baseUrl"/>, and ends the path with
    /// <c>/</c>.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Credentials go: they would otherwise reach every request URI, every page link built from
    /// it, and any log that prints either. Authentication belongs in a header.</item>
    /// <item>A query and a fragment go: a base address with one never resolves a relative path below
    /// its own path, so <c>https://dev.azure.com/myorg?x=1</c> would send requests to
    /// <c>https://dev.azure.com/_apis/…</c>.</item>
    /// <item>The trailing <c>/</c> is what keeps the last path segment when a relative path is resolved
    /// against the base: without it <c>https://dev.azure.com/myorg</c> also loses <c>myorg</c>.</item>
    /// </list>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="baseUrl"/> is null, empty or white space.</exception>
    /// <exception cref="UriFormatException"><paramref name="baseUrl"/> is not an absolute URL.</exception>
    public static Uri Create(string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        // On Linux "/myorg" parses as an absolute file URI, so the scheme is checked too.
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new UriFormatException("The base URL must be an absolute http or https URL.");
        }

        var address = uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);

        return new Uri(address.EndsWith('/') ? address : address + "/");
    }
}
