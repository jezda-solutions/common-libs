using System.Net;
using Jezda.Common.Integrations.Abstractions;
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

    [Fact]
    public async Task SearchTasksAsync_UsesSearchApiInOneRequest()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { total_count = 0, items = Array.Empty<object>() });

        // Deliberately called through the interface. SearchTasksAsync is a default interface
        // implementation that this class overrides implicitly — there is no `override` keyword and
        // no compiler diagnostic, so if the interface signature ever changes the concrete method
        // silently stops implementing it and every caller falls back to the fan-out. Through the
        // interface, the single-request assertion below is what catches that.
        await ((IExternalTaskProvider)_provider).SearchTasksAsync("token", "webhook");

        // The whole point of the override: one call, not GetProjects + GetTasks per repository.
        Assert.Single(_handler.SentRequests);

        var requestUri = _handler.SentRequests[0].RequestUri!;
        Assert.Contains("search/issues", requestUri.AbsolutePath);

        var query = Uri.UnescapeDataString(requestUri.Query);
        Assert.Contains("webhook", query);
        Assert.Contains("is:issue", query);
        Assert.Contains("involves:@me", query);
    }

    [Fact]
    public async Task SearchTasksAsync_DerivesProjectIdFromRepositoryUrl()
    {
        // ProjectId must be the owner/repo full name GetProjectsAsync reports, because consumers
        // key stored rows on that value.
        var response = new
        {
            total_count = 1,
            items = new object[]
            {
                new
                {
                    number = 412,
                    title = "Fix webhook retry",
                    state = "open",
                    html_url = "https://github.com/jezda-solutions/serp/issues/412",
                    repository_url = "https://api.github.com/repos/jezda-solutions/serp"
                }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, response);

        var result = await _provider.SearchTasksAsync("token", "webhook");

        var task = Assert.Single(result);
        Assert.Equal("412", task.Id);
        Assert.Equal("Fix webhook retry", task.Title);
        Assert.Equal("open", task.Status);
        Assert.Equal("https://github.com/jezda-solutions/serp/issues/412", task.Url);
        Assert.Equal("jezda-solutions/serp", task.ProjectId);
        Assert.Equal(ExternalProvider.GitHub, task.Provider);
    }

    [Fact]
    public async Task SearchTasksAsync_ExcludesPullRequests()
    {
        var response = new
        {
            total_count = 2,
            items = new object[]
            {
                new
                {
                    number = 1,
                    title = "Real issue",
                    state = "open",
                    html_url = "https://github.com/owner/repo/issues/1",
                    repository_url = "https://api.github.com/repos/owner/repo"
                },
                new
                {
                    number = 2,
                    title = "A pull request",
                    state = "open",
                    html_url = "https://github.com/owner/repo/pull/2",
                    repository_url = "https://api.github.com/repos/owner/repo",
                    pull_request = new { url = "https://api.github.com/repos/owner/repo/pulls/2" }
                }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, response);

        var result = await _provider.SearchTasksAsync("token", "issue");

        var task = Assert.Single(result);
        Assert.Equal("1", task.Id);
    }

    [Fact]
    public async Task SearchTasksAsync_TrimsToLimit()
    {
        var items = Enumerable.Range(1, 10).Select(n => (object)new
        {
            number = n,
            title = $"Issue {n}",
            state = "open",
            html_url = $"https://github.com/owner/repo/issues/{n}",
            repository_url = "https://api.github.com/repos/owner/repo"
        }).ToArray();
        _handler.EnqueueResponse(HttpStatusCode.OK, new { total_count = 10, items });

        var result = await _provider.SearchTasksAsync("token", "issue", limit: 3);

        Assert.Equal(3, result.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchTasksAsync_BlankTerm_ReturnsEmptyWithoutCallingGitHub(string term)
    {
        // A blank term must never become "every issue I am involved in".
        var result = await _provider.SearchTasksAsync("token", term);

        Assert.Empty(result);
        Assert.Empty(_handler.SentRequests);
    }
}
