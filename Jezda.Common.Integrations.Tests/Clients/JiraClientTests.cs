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
}
