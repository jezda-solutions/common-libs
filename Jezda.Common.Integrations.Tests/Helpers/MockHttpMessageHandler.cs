using System.Net;
using System.Text;
using System.Text.Json;

namespace Jezda.Common.Integrations.Tests.Helpers;

public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<MockResponse> _responses = new();

    public List<HttpRequestMessage> SentRequests { get; } = [];

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

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        SentRequests.Add(request);

        if (_responses.Count == 0)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("No mock response configured")
            });
        }

        var mock = _responses.Dequeue();

        var response = new HttpResponseMessage(mock.StatusCode)
        {
            Content = new StringContent(mock.Body, Encoding.UTF8, "application/json")
        };

        if (mock.Header is { } header)
        {
            response.Headers.Add(header.Name, header.Value);
        }

        return Task.FromResult(response);
    }

    private sealed record MockResponse(HttpStatusCode StatusCode, string Body, (string Name, string Value)? Header = null);
}
