using System.Net;
using System.Text;
using System.Text.Json;

namespace Jezda.Common.Integrations.Tests.Helpers;

public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Lock _gate = new();
    private readonly Queue<MockResponse> _responses = new();
    private readonly Dictionary<string, MockResponse> _responsesByPath = new(StringComparer.Ordinal);
    private int _inFlight;

    public List<HttpRequestMessage> SentRequests { get; } = [];

    /// <summary>The most requests that were in flight at once.</summary>
    public int MaxInFlight { get; private set; }

    /// <summary>
    /// Answers every request to one path with the same response, whatever order they arrive in, for
    /// requests a provider sends concurrently. Takes precedence over the queued responses.
    /// </summary>
    /// <param name="absolutePath">The request's path, such as <c>/repos/owner/a</c>.</param>
    /// <param name="response">Builds the response, once per request.</param>
    /// <param name="delay">How long the answer takes, so a test can overlap requests.</param>
    public void RespondTo(string absolutePath, Func<HttpResponseMessage> response, TimeSpan delay = default)
    {
        _responsesByPath[absolutePath] = new MockResponse(default, string.Empty, Build: response, Delay: delay);
    }

    /// <summary>
    /// Answers every request to one path with a JSON body. See the overload taking a response builder.
    /// </summary>
    public void RespondTo(string absolutePath, HttpStatusCode statusCode, object? body = null, TimeSpan delay = default)
    {
        var json = body is not null ? JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) : "{}";
        _responsesByPath[absolutePath] = new MockResponse(statusCode, json, Delay: delay);
    }

    public void EnqueueResponse(HttpStatusCode statusCode, object? body = null)
    {
        var json = body is not null ? JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }) : "{}";
        _responses.Enqueue(new MockResponse(statusCode, json));
    }

    public void EnqueueResponse(HttpStatusCode statusCode, string json)
    {
        _responses.Enqueue(new MockResponse(statusCode, json));
    }

    /// <summary>
    /// Queues a JSON body with one response header, for the paging headers providers follow.
    /// </summary>
    public void EnqueueResponseWithHeader(object body, string headerName, string headerValue)
    {
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        _responses.Enqueue(new MockResponse(HttpStatusCode.OK, json, (headerName, headerValue)));
    }

    /// <summary>
    /// Queues a response built by the test, for statuses, media types and headers the JSON helpers
    /// do not cover: a sign-in page, a redirect.
    /// </summary>
    public void EnqueueResponse(Func<HttpResponseMessage> response)
    {
        _responses.Enqueue(new MockResponse(default, string.Empty, Build: response));
    }

    /// <summary>
    /// Queues a failure to send, the way a dropped connection or a timeout surfaces.
    /// </summary>
    public void EnqueueException(Exception exception)
    {
        _responses.Enqueue(new MockResponse(default, string.Empty, Build: () => throw exception));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        MockResponse? mock;

        lock (_gate)
        {
            SentRequests.Add(request);
            MaxInFlight = Math.Max(MaxInFlight, ++_inFlight);

            if (!_responsesByPath.TryGetValue(request.RequestUri!.AbsolutePath, out mock))
            {
                _responses.TryDequeue(out mock);
            }
        }

        try
        {
            if (mock is null)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("No mock response configured")
                };
            }

            if (mock.Delay > TimeSpan.Zero)
            {
                await Task.Delay(mock.Delay, cancellationToken);
            }

            if (mock.Build is not null)
            {
                var built = mock.Build();
                built.RequestMessage ??= request;
                return built;
            }

            var response = new HttpResponseMessage(mock.StatusCode)
            {
                Content = new StringContent(mock.Body, Encoding.UTF8, "application/json")
            };

            if (mock.Header is { } header)
            {
                response.Headers.Add(header.Name, header.Value);
            }

            return response;
        }
        finally
        {
            lock (_gate)
            {
                _inFlight--;
            }
        }
    }

    private sealed record MockResponse(
        HttpStatusCode StatusCode,
        string Body,
        (string Name, string Value)? Header = null,
        Func<HttpResponseMessage>? Build = null,
        TimeSpan Delay = default);
}
