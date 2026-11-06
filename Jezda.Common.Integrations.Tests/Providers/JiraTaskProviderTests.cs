using System.Net;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Jira.Providers;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Providers;

public class JiraTaskProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly JiraTaskProvider _provider;

    public JiraTaskProviderTests()
    {
        // No base address — JiraTaskProvider sets it per-request from baseUrl parameter
        var factory = new MockHttpClientFactory(_handler, JiraTaskProvider.HttpClientName);
        _provider = new JiraTaskProvider(factory, NullLogger<JiraTaskProvider>.Instance);
    }

    [Fact]
    public void Provider_ReturnsJira()
    {
        Assert.Equal(ExternalProvider.Jira, _provider.Provider);
    }

    [Fact]
    public void FormatAccessToken_CombinesEmailAndToken()
    {
        var result = JiraTaskProvider.FormatAccessToken("user@example.com", "api-token-123");
        Assert.Equal("user@example.com:api-token-123", result);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithValidToken_ReturnsTrue()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { accountId = "123", displayName = "Test User" });

        var result = await _provider.ValidateConnectionAsync("user@test.com:token", "https://test.atlassian.net/");

        Assert.True(result);
        Assert.Single(_handler.SentRequests);
        Assert.Equal("Basic", _handler.SentRequests[0].Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithInvalidToken_ReturnsFalse()
    {
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        var result = await _provider.ValidateConnectionAsync("bad:token", "https://test.atlassian.net/");

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithoutBaseUrl_ThrowsArgumentException()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            _provider.ValidateConnectionAsync("token", baseUrl: null));
    }

    [Fact]
    public async Task GetProjectsAsync_ReturnsExternalProjectDtos()
    {
        var projects = new[]
        {
            new { id = "10001", key = "PROJ", name = "Project One" },
            new { id = "10002", key = "TEST", name = "Test Project" }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, projects);

        var result = await _provider.GetProjectsAsync("user@test.com:token", "https://test.atlassian.net/");

        Assert.Equal(2, result.Count);
        Assert.Equal("10001", result[0].Id);
        Assert.Equal("PROJ", result[0].Path);
        Assert.Equal("Project One", result[0].Name);
        Assert.Equal("https://test.atlassian.net/browse/PROJ", result[0].Url);
        Assert.Equal(ExternalProvider.Jira, result[0].Provider);
    }

    [Fact]
    public async Task GetTasksAsync_ReturnsExternalTaskDtos()
    {
        var searchResponse = new
        {
            startAt = 0,
            maxResults = 50,
            total = 2,
            issues = new[]
            {
                new { id = "10001", key = "PROJ-1", fields = new { summary = "Fix bug", status = new { name = "In Progress" } } },
                new { id = "10002", key = "PROJ-2", fields = new { summary = "Add feature", status = new { name = "To Do" } } }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, searchResponse);

        var result = await _provider.GetTasksAsync("user@test.com:token", "PROJ", "https://test.atlassian.net/");

        Assert.Equal(2, result.Count);
        Assert.Equal("PROJ-1", result[0].Id);
        Assert.Equal("Fix bug", result[0].Title);
        Assert.Equal("In Progress", result[0].Status);
        Assert.Equal("https://test.atlassian.net/browse/PROJ-1", result[0].Url);
        Assert.Equal("PROJ", result[0].ProjectId);
        Assert.Equal("PROJ", result[0].ProjectPath);
        Assert.Equal(ExternalProvider.Jira, result[0].Provider);
    }

    [Fact]
    public async Task GetProjectsAsync_MapsWhatTheListReports()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new object[]
        {
            new { id = "1", key = "A", name = "Reported", archived = true, isPrivate = false },
            new { id = "2", key = "B", name = "Silent" }
        });

        var result = await _provider.GetProjectsAsync("user@test.com:token", "https://test.atlassian.net/");

        Assert.True(result[0].IsArchived);
        Assert.False(result[0].IsPrivate);
        Assert.Null(result[1].IsArchived);
        Assert.Null(result[1].IsPrivate);
        Assert.Null(result[0].CreatedAt);
        Assert.Null(result[0].UpdatedAt);
    }

    [Theory]
    [InlineData("10001", "project = 10001")]
    [InlineData("PROJ", "project = \"PROJ\"")]
    public async Task GetTasksAsync_ReadsTheProjectOffEachIssue(string projectId, string expectedJql)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            issues = new[]
            {
                new
                {
                    id = "1",
                    key = "PROJ-1",
                    fields = new
                    {
                        summary = "Fix bug",
                        status = new { name = "To Do" },
                        project = new { id = "10001", key = "PROJ", name = "Project One" }
                    }
                }
            }
        });

        var result = await _provider.GetTasksAsync("user@test.com:token", projectId, "https://test.atlassian.net/");

        var task = Assert.Single(result);
        Assert.Equal("10001", task.ProjectId);
        Assert.Equal("PROJ", task.ProjectPath);

        using var body = System.Text.Json.JsonDocument.Parse(await _handler.SentRequests[0].Content!.ReadAsStringAsync());
        Assert.Equal(expectedJql, body.RootElement.GetProperty("jql").GetString());
        Assert.Contains("project", body.RootElement.GetProperty("fields").EnumerateArray().Select(f => f.GetString()));
    }
}
