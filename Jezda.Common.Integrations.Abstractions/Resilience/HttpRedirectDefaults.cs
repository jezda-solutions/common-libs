using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jezda.Common.Integrations.Abstractions.Resilience;

public static class HttpRedirectDefaults
{
    /// <summary>
    /// Stops the client's primary handler from following redirects, and warns when it cannot.
    /// </summary>
    /// <remarks>
    /// .NET drops the <c>Authorization</c> header when it follows a redirect, so a base URL that
    /// answers 302 (a site renamed, a stored URL in an old form) would answer the next hop with 401
    /// even for a valid token, and read as an expired one. Not following it hands the redirect itself
    /// to the caller, which has to treat a 3xx as a failure.
    /// <para />
    /// A warning, not an exception: a custom handler may not follow redirects at all, and only its
    /// author can tell. A primary handler configured after this call replaces this one and is not seen
    /// here.
    /// </remarks>
    public static IHttpClientBuilder DisableAutoRedirect(this IHttpClientBuilder builder) =>
        builder.ConfigurePrimaryHttpMessageHandler(DisableAutoRedirect);

    private static void DisableAutoRedirect(HttpMessageHandler primaryHandler, IServiceProvider services)
    {
        switch (primaryHandler)
        {
            case SocketsHttpHandler sockets:
                sockets.AllowAutoRedirect = false;
                break;
            case HttpClientHandler client:
                client.AllowAutoRedirect = false;
                break;
            default:
                services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(HttpRedirectDefaults)).LogWarning(
                    "Cannot turn off redirects on primary handler {HandlerType}. If it follows them, a valid token can read as expired after a redirect",
                    primaryHandler.GetType().FullName);
                break;
        }
    }
}
