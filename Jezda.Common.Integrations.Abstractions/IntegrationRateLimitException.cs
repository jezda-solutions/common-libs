using System.Net;

namespace Jezda.Common.Integrations.Abstractions;

/// <summary>
/// A provider refused a request because of a rate limit. <see cref="HttpRequestException.StatusCode"/>
/// is always 429, even where the provider answered 403, so the refusal does not read as a rejected token.
/// </summary>
/// <remarks>
/// It derives from <see cref="HttpRequestException"/>, so code that catches that and branches on the
/// status keeps working. Catch this type to learn when to try again.
/// </remarks>
public sealed class IntegrationRateLimitException : HttpRequestException
{
    public IntegrationRateLimitException(string message, DateTimeOffset? retryAt)
        : base(message, inner: null, statusCode: HttpStatusCode.TooManyRequests)
    {
        RetryAt = retryAt;
    }

    /// <summary>
    /// The earliest time the provider said it accepts requests again, or null when its answer did
    /// not say.
    /// </summary>
    public DateTimeOffset? RetryAt { get; }
}
