using System.Net;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.GitHub.Providers;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Providers;

public class GitHubTaskProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly GitHubTaskProvider _provider;

    public GitHubTaskProviderTests()
    {
        var factory = new MockHttpClientFactory(
            _handler,
            GitHubTaskProvider.HttpClientName,
            new Uri("https://api.github.com/"));

        _provider = new GitHubTaskProvider(factory, NullLogger<GitHubTaskProvider>.Instance);
    }

    [Fact]
    public void Provider_ReturnsGitHub()
    {
        Assert.Equal(ExternalProvider.GitHub, _provider.Provider);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithValidToken_ReturnsTrue()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { login = "testuser" });

        var result = await _provider.ValidateConnectionAsync("valid-token");

        Assert.True(result);
        Assert.Single(_handler.SentRequests);
        Assert.Equal("Bearer", _handler.SentRequests[0].Headers.Authorization?.Scheme);
        Assert.Equal("valid-token", _handler.SentRequests[0].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithInvalidToken_ReturnsFalse()
    {
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        var result = await _provider.ValidateConnectionAsync("bad-token");

        Assert.False(result);
    }

    [Fact]
    public async Task GetProjectsAsync_ReturnsExternalProjectDtos()
    {
        var repos = new object[]
        {
            new { id = 1L, name = "repo1", full_name = "owner/repo1", html_url = "https://github.com/owner/repo1", description = "Desc 1" },
            new { id = 2L, name = "repo2", full_name = "owner/repo2", html_url = "https://github.com/owner/repo2", description = "" }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, repos);

        var result = await _provider.GetProjectsAsync("token");

        Assert.Equal(2, result.Count);
        Assert.Equal("owner/repo1", result[0].Id);
        Assert.Equal("repo1", result[0].Name);
        Assert.Equal("Desc 1", result[0].Description);
        Assert.Equal("https://github.com/owner/repo1", result[0].Url);
        Assert.Equal(ExternalProvider.GitHub, result[0].Provider);
        Assert.Equal("", result[1].Description);
    }

    [Fact]
    public async Task GetTasksAsync_ReturnsOpenAndClosedTaskDtos()
    {
        var issues = new[]
        {
            new { id = 100L, number = 1, title = "Bug fix", state = "open", html_url = "https://github.com/owner/repo/issues/1" },
            new { id = 101L, number = 2, title = "Done feature", state = "closed", html_url = "https://github.com/owner/repo/issues/2" }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, issues);

        var result = await _provider.GetTasksAsync("token", "owner/repo");

        Assert.Equal(2, result.Count);
        Assert.Equal("1", result[0].Id);
        Assert.Equal("Bug fix", result[0].Title);
        Assert.Equal("open", result[0].Status);
        Assert.Equal("owner/repo", result[0].ProjectId);
        Assert.Equal(ExternalProvider.GitHub, result[0].Provider);
        Assert.Equal("closed", result[1].Status);
    }

    [Fact]
    public async Task GetTasksAsync_ExcludesPullRequests()
    {
        var items = new object[]
        {
            new { id = 100L, number = 1, title = "Real issue", state = "open", html_url = "https://github.com/owner/repo/issues/1" },
            new { id = 101L, number = 2, title = "A pull request", state = "open", html_url = "https://github.com/owner/repo/pull/2", pull_request = new { url = "https://api.github.com/repos/owner/repo/pulls/2" } }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, items);

        var result = await _provider.GetTasksAsync("token", "owner/repo");

        Assert.Single(result);
        Assert.Equal("1", result[0].Id);
        Assert.Equal("Real issue", result[0].Title);
    }

    [Fact]
    public async Task GetTasksAsync_RequestsAllStatesWithMaxPageSize()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Array.Empty<object>());

        await _provider.GetTasksAsync("token", "owner/repo");

        var requestUri = _handler.SentRequests[0].RequestUri!;
        var queryParams = requestUri.Query.TrimStart('?').Split('&')
            .ToDictionary(s => s.Split('=')[0], s => s.Split('=')[1]);
        Assert.Equal("all", queryParams["state"]);
        Assert.Equal("100", queryParams["per_page"]);
        Assert.Contains("repos/owner/repo/issues", requestUri.AbsolutePath);
    }
}
