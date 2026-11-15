using System;
using System.Net;
using System.Net.Http;

namespace Jezda.Common.Extensions;

/// <summary>
/// Thrown by <see cref="HttpResponseMessageExtensions.ReadApiResponseDataAsync{T}"/> for a failed status.
/// Derives from <see cref="HttpRequestException"/>, so existing handlers keep catching it.
/// </summary>
public sealed class ApiResponseException : HttpRequestException
{
    /// <summary>The most characters of the error body kept in <see cref="ResponseBody"/>.</summary>
    public const int MaxBodyLength = 4096;

    public ApiResponseException(HttpStatusCode statusCode, string? responseBody)
        : base($"Response status code does not indicate success: {(int)statusCode} ({statusCode}).", inner: null, statusCode)
    {
        ResponseBody = responseBody;
    }

    /// <summary>
    /// The error body, read as UTF-8 and cut to <see cref="MaxBodyLength"/> characters, for example an
    /// RFC 9457 problem+json whose <c>code</c> a caller can branch on. Null when there is no body or it
    /// could not be read. Never part of <see cref="Exception.Message"/>, so logging the message or
    /// <see cref="Exception.ToString"/> does not write it. A tool that also records an exception's public
    /// properties (a structured-logging destructurer, an error-reporting SDK) does write it, and an error body
    /// can echo request data, so mask or exclude this property in such a tool's configuration.
    /// </summary>
    public string? ResponseBody { get; }
}
