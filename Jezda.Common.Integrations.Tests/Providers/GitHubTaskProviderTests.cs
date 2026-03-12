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
    public async Task GetTasksAsync_ReturnsExternalTaskDtos()
    {
        var issues = new[]
        {
            new { id = 100L, number = 1, title = "Bug fix", state = "open", html_url = "https://github.com/owner/repo/issues/1" },
            new { id = 101L, number = 2, title = "Feature request", state = "open", html_url = "https://github.com/owner/repo/issues/2" }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, issues);

        var result = await _provider.GetTasksAsync("token", "owner/repo");

        Assert.Equal(2, result.Count);
        Assert.Equal("1", result[0].Id);
        Assert.Equal("Bug fix", result[0].Title);
        Assert.Equal("open", result[0].Status);
        Assert.Equal("owner/repo", result[0].ProjectId);
        Assert.Equal(ExternalProvider.GitHub, result[0].Provider);
    }
}
