using System.Net;
using Jezda.Common.Integrations.Jira;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Clients;

public class JiraClientTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly JiraClient _client;

    public JiraClientTests()
    {
        var httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://old.atlassian.net/") };
        _client = new JiraClient(httpClient, Options.Create(new JiraOptions()), NullLogger<JiraClient>.Instance);
    }

    [Fact]
    public async Task GetProjectsAsync_Redirect_ThrowsWithTheStatusAndTarget()
    {
        _handler.EnqueueResponse(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri("https://new.atlassian.net/rest/api/3/project");
            return response;
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetProjectsAsync());

        Assert.Equal(HttpStatusCode.Found, ex.StatusCode);
        Assert.Contains("https://new.atlassian.net/rest/api/3/project", ex.Message);
    }

    [Fact]
    public async Task SearchIssuesAsync_HtmlOnSuccess_ThrowsHttpRequestException()
    {
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html></html>", System.Text.Encoding.UTF8, "text/html")
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.SearchIssuesAsync("project = PROJ"));

        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
    }

    [Fact]
    public async Task SearchIssuesAsync_PostsToSearchJqlWithThePageToken()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            issues = new[] { new { id = "1", key = "PROJ-1" } },
            nextPageToken = "page-3",
            isLast = false
        });

        var page = await _client.SearchIssuesAsync("project = PROJ", nextPageToken: "page-2", maxResults: 10);

        var request = Assert.Single(_handler.SentRequests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/rest/api/3/search/jql", request.RequestUri!.AbsolutePath);

        using var body = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
        Assert.Equal("page-2", body.RootElement.GetProperty("nextPageToken").GetString());
        Assert.Equal(10, body.RootElement.GetProperty("maxResults").GetInt32());

        Assert.Equal("page-3", page!.NextPageToken);
        Assert.False(page.IsLast);
        Assert.Equal("PROJ-1", Assert.Single(page.Issues).Key);
    }

    [Fact]
    public async Task SearchIssuesAsync_FirstPage_SendsNoToken()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { issues = Array.Empty<object>(), isLast = true });

        await _client.SearchIssuesAsync("project = PROJ");

        using var body = System.Text.Json.JsonDocument.Parse(await _handler.SentRequests[0].Content!.ReadAsStringAsync());
        Assert.False(body.RootElement.TryGetProperty("nextPageToken", out _));
    }
}
