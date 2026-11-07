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

        Assert.True(result.IsValid);
        Assert.Single(_handler.SentRequests);
        Assert.Equal("Bearer", _handler.SentRequests[0].Headers.Authorization?.Scheme);
        Assert.Equal("valid-token", _handler.SentRequests[0].Headers.Authorization?.Parameter);
    }

    [Theory]
    [InlineData("token\u201D")] // A pasted smart quote
    [InlineData("tok\u00E9n")]
    [InlineData("token\n")] // A pasted line break
    [InlineData("token\r\n")]
    public async Task ValidateConnectionAsync_TokenThatCannotGoInAHeader_ReturnsFalseWithoutSending(string token)
    {
        var result = await _provider.ValidateConnectionAsync(token);

        Assert.False(result.IsValid);
        Assert.Equal(TokenRejectionReason.NotSendable, result.Reason);
        Assert.Empty(_handler.SentRequests);
    }

    [Fact]
    public async Task OtherCalls_TokenThatCannotGoInAHeader_ThrowWithoutSending()
    {
        // Sent, it would fail in the HTTP stack as an outage, be retried and count towards the
        // circuit breaker every caller of this provider shares.
        const string token = "token\u201D";

        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetProjectsAsync(token));
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetTasksAsync(token, "123"));
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.SearchTasksAsync(token, "bug"));
        Assert.Empty(_handler.SentRequests);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithInvalidToken_ReturnsFalse()
    {
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        var result = await _provider.ValidateConnectionAsync("bad-token");

        Assert.False(result.IsValid);
        Assert.Equal(TokenRejectionReason.InvalidToken, result.Reason);
    }

    [Theory]
    [InlineData("""{"message":"Maximum number of login attempts exceeded. Please try again later."}""")]
    [InlineData("<html>Blocked by proxy</html>")]
    public async Task ValidateConnectionAsync_Forbidden_ThrowsInsteadOfBlamingTheToken(string body)
    {
        _handler.EnqueueResponse(HttpStatusCode.Forbidden, body);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.ValidateConnectionAsync("token"));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ValidateConnectionAsync_OtherFailureStatus_ThrowsInsteadOfBlamingTheToken(HttpStatusCode status)
    {
        _handler.EnqueueResponse(status);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.ValidateConnectionAsync("token"));

        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task ValidateConnectionAsync_ConnectionFailure_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueException(new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<HttpRequestException>(() => _provider.ValidateConnectionAsync("token"));
    }

    [Fact]
    public async Task ValidateConnectionAsync_Timeout_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueException(new TaskCanceledException("The request timed out"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _provider.ValidateConnectionAsync("token"));
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
        Assert.Equal("1", result[0].Id);
        Assert.Equal("owner/repo1", result[0].Path);
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
        _handler.EnqueueResponse(HttpStatusCode.OK, Repo());
        _handler.EnqueueResponse(HttpStatusCode.OK, issues);

        var result = await _provider.GetTasksAsync("token", "owner/repo");

        Assert.Equal(2, result.Count);
        Assert.Equal("1", result[0].Id);
        Assert.Equal("Bug fix", result[0].Title);
        Assert.Equal("open", result[0].Status);
        Assert.Equal("42", result[0].ProjectId);
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
        _handler.EnqueueResponse(HttpStatusCode.OK, Repo());
        _handler.EnqueueResponse(HttpStatusCode.OK, items);

        var result = await _provider.GetTasksAsync("token", "owner/repo");

        Assert.Single(result);
        Assert.Equal("1", result[0].Id);
        Assert.Equal("Real issue", result[0].Title);
    }

    [Fact]
    public async Task GetTasksAsync_RequestsAllStatesWithMaxPageSize()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Repo());
        _handler.EnqueueResponse(HttpStatusCode.OK, Array.Empty<object>());

        await _provider.GetTasksAsync("token", "owner/repo");

        var requestUri = _handler.SentRequests[1].RequestUri!;
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
        // ProjectId must be the durable id GetProjectsAsync reports, because consumers key stored
        // rows on that value, and a search item only names its repository.
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
        _handler.EnqueueResponse(HttpStatusCode.OK, Repo(77, "jezda-solutions/serp"));

        var result = await _provider.SearchTasksAsync("token", "webhook");

        var task = Assert.Single(result);
        Assert.Equal("412", task.Id);
        Assert.Equal("Fix webhook retry", task.Title);
        Assert.Equal("open", task.Status);
        Assert.Equal("https://github.com/jezda-solutions/serp/issues/412", task.Url);
        Assert.Equal("77", task.ProjectId);
        Assert.Equal("/repos/jezda-solutions/serp", _handler.SentRequests[1].RequestUri!.AbsolutePath);
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
        _handler.EnqueueResponse(HttpStatusCode.OK, Repo());

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
        _handler.EnqueueResponse(HttpStatusCode.OK, Repo());

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

    [Fact]
    public async Task GetProjectsAsync_FollowsLinkHeaderAcrossPages()
    {
        _handler.EnqueueResponseWithHeader(
            new[] { Repo(1, "owner/first") },
            "Link",
            "<https://api.github.com/user/repos?per_page=100&page=2>; rel=\"next\", <https://api.github.com/user/repos?per_page=100&page=2>; rel=\"last\"");
        _handler.EnqueueResponse(HttpStatusCode.OK, new[] { Repo(2, "owner/second") });

        var result = await _provider.GetProjectsAsync("token");

        Assert.Equal(["1", "2"], result.Select(p => p.Id));
        Assert.Equal(["owner/first", "owner/second"], result.Select(p => p.Path));
        Assert.Equal(2, _handler.SentRequests.Count);
        Assert.Equal("?per_page=100", _handler.SentRequests[0].RequestUri!.Query);
        Assert.Equal("?per_page=100&page=2", _handler.SentRequests[1].RequestUri!.Query);
    }

    [Fact]
    public async Task GetProjectsAsync_MapsMetadata()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new object[]
        {
            new
            {
                id = 5L,
                name = "repo",
                full_name = "owner/repo",
                html_url = "https://github.com/owner/repo",
                @private = true,
                archived = true,
                created_at = "2020-01-02T03:04:05Z",
                updated_at = "2024-01-01T00:00:00Z",
                pushed_at = "2025-01-01T00:00:00Z"
            }
        });

        var project = Assert.Single(await _provider.GetProjectsAsync("token"));

        Assert.Equal(DateTimeOffset.Parse("2020-01-02T03:04:05Z"), project.CreatedAt);
        Assert.Equal(DateTimeOffset.Parse("2025-01-01T00:00:00Z"), project.UpdatedAt);
        Assert.True(project.IsArchived);
        Assert.True(project.IsPrivate);
    }

    [Theory]
    [InlineData("2024-06-01T00:00:00Z")] // metadata changed after the last push
    [InlineData(null)] // nothing was ever pushed
    public async Task GetProjectsAsync_UpdatedAtFallsBackToMetadataChange(string? pushedAt)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new object[]
        {
            new { id = 5L, name = "repo", full_name = "owner/repo", html_url = "u", updated_at = "2025-01-01T00:00:00Z", pushed_at = pushedAt }
        });

        var project = Assert.Single(await _provider.GetProjectsAsync("token"));

        Assert.Equal(DateTimeOffset.Parse("2025-01-01T00:00:00Z"), project.UpdatedAt);
    }

    [Fact]
    public async Task GetTasksAsync_ByDurableId_ReadsTheRepositoryByIdAndItsIssuesByCurrentPath()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Repo(42, "owner/renamed"));
        _handler.EnqueueResponse(HttpStatusCode.OK, new[]
        {
            new { id = 100L, number = 1, title = "Bug", state = "open", html_url = "https://github.com/owner/renamed/issues/1" }
        });

        var result = await _provider.GetTasksAsync("token", "42");

        Assert.Equal("/repositories/42", _handler.SentRequests[0].RequestUri!.AbsolutePath);
        Assert.Equal("/repos/owner/renamed/issues", _handler.SentRequests[1].RequestUri!.AbsolutePath);

        var task = Assert.Single(result);
        Assert.Equal("42", task.ProjectId);
    }

    [Fact]
    public async Task GetTasksAsync_ByPath_StillReturnsTheDurableId()
    {
        // A row stored by an older version holds owner/repo and must keep working until migrated.
        _handler.EnqueueResponse(HttpStatusCode.OK, Repo(42, "owner/repo"));
        _handler.EnqueueResponse(HttpStatusCode.OK, new[]
        {
            new { id = 100L, number = 1, title = "Bug", state = "open", html_url = "https://github.com/owner/repo/issues/1" }
        });

        var result = await _provider.GetTasksAsync("token", "owner/repo");

        Assert.Equal("/repos/owner/repo", _handler.SentRequests[0].RequestUri!.AbsolutePath);
        Assert.Equal("42", Assert.Single(result).ProjectId);
    }

    [Fact]
    public async Task GetTasksAsync_FollowsLinkHeaderAcrossPages()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Repo());
        _handler.EnqueueResponseWithHeader(
            new[] { new { id = 1L, number = 1, title = "One", state = "open", html_url = "u" } },
            "Link",
            "<https://api.github.com/repos/owner/repo/issues?page=2>; rel=\"next\"");
        _handler.EnqueueResponse(HttpStatusCode.OK, new[] { new { id = 2L, number = 2, title = "Two", state = "open", html_url = "u" } });

        var result = await _provider.GetTasksAsync("token", "owner/repo");

        Assert.Equal(["1", "2"], result.Select(t => t.Id));
    }

    [Fact]
    public async Task GetTasksAsync_UnknownRepository_KeepsTheStatusCode()
    {
        _handler.EnqueueResponse(HttpStatusCode.NotFound);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetTasksAsync("token", "42"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetTasksAsync_BlankProject_ThrowsWithoutCallingGitHub(string projectId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetTasksAsync("token", projectId));

        Assert.Empty(_handler.SentRequests);
    }

    [Fact]
    public async Task SearchTasksAsync_LooksUpEachRepositoryOnce()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            total_count = 3,
            items = new[] { SearchItem(1, "owner/a"), SearchItem(2, "owner/b"), SearchItem(3, "owner/a") }
        });
        _handler.RespondTo("/repos/owner/a", HttpStatusCode.OK, Repo(10, "owner/a"));
        _handler.RespondTo("/repos/owner/b", HttpStatusCode.OK, Repo(20, "owner/b"));

        var result = await _provider.SearchTasksAsync("token", "bug");

        Assert.Equal(3, _handler.SentRequests.Count);
        Assert.Equal(["10", "20", "10"], result.Select(t => t.ProjectId));
    }

    [Fact]
    public async Task SearchTasksAsync_RepositoryThatAnswers404_DropsOnlyItsItems()
    {
        // The search index can lag behind a deleted repository. Without a durable id the item cannot
        // be bound, and one stale hit must not fail the whole picker.
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            total_count = 2,
            items = new[] { SearchItem(1, "owner/gone"), SearchItem(2, "owner/kept") }
        });
        _handler.RespondTo("/repos/owner/gone", HttpStatusCode.NotFound);
        _handler.RespondTo("/repos/owner/kept", HttpStatusCode.OK, Repo(20, "owner/kept"));

        var result = await _provider.SearchTasksAsync("token", "bug");

        Assert.Equal("2", Assert.Single(result).Id);
    }

    [Fact]
    public async Task SearchTasksAsync_UnauthorizedLookup_KeepsTheStatusCode()
    {
        // Consumers flag an integration for re-authentication by matching 401/403.
        _handler.EnqueueResponse(HttpStatusCode.OK, new { total_count = 1, items = new[] { SearchItem(1, "owner/a") } });
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.SearchTasksAsync("token", "bug"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.UnavailableForLegalReasons)]
    public async Task SearchTasksAsync_RepositoryRefusedForItsOwnReasons_DropsOnlyItsItems(HttpStatusCode status)
    {
        // A SAML requirement, an IP allow list or a blocked repository says nothing about the token.
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            total_count = 2,
            items = new[] { SearchItem(1, "owner/refused"), SearchItem(2, "owner/kept") }
        });
        _handler.RespondTo("/repos/owner/refused", status, new { message = "Resource protected by organization SAML enforcement." });
        _handler.RespondTo("/repos/owner/kept", HttpStatusCode.OK, Repo(20, "owner/kept"));

        var result = await _provider.SearchTasksAsync("token", "bug");

        Assert.Equal("2", Assert.Single(result).Id);
    }

    [Theory]
    [MemberData(nameof(RateLimitResponses))]
    public async Task SearchTasksAsync_RateLimitedLookup_ThrowsTooManyRequests(RateLimitResponse limited)
    {
        // Consumers read 403 as an expired token, and a rate limit is no reason to sign in again.
        _handler.EnqueueResponse(HttpStatusCode.OK, new { total_count = 1, items = new[] { SearchItem(1, "owner/a") } });
        _handler.EnqueueResponse(limited.Build);

        var ex = await Assert.ThrowsAsync<IntegrationRateLimitException>(() => _provider.SearchTasksAsync("token", "bug"));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    [Theory]
    [MemberData(nameof(RateLimitResponses))]
    public async Task SearchTasksAsync_RateLimitedSearch_ThrowsTooManyRequests(RateLimitResponse limited)
    {
        // The search API allows 30 requests a minute, so it runs out long before the lookups do.
        _handler.EnqueueResponse(limited.Build);

        var ex = await Assert.ThrowsAsync<IntegrationRateLimitException>(() => _provider.SearchTasksAsync("token", "bug"));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    [Fact]
    public async Task SearchTasksAsync_SearchForbiddenWithoutARateLimit_KeepsTheStatusCode()
    {
        _handler.EnqueueResponse(HttpStatusCode.Forbidden, new { message = "Must have admin rights to Repository." });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.SearchTasksAsync("token", "bug"));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Fact]
    public async Task SearchTasksAsync_LimitsConcurrentLookups()
    {
        var names = Enumerable.Range(1, 12).Select(i => $"owner/r{i}").ToArray();
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            total_count = names.Length,
            items = names.Select((name, i) => SearchItem(i + 1, name)).ToArray()
        });

        foreach (var (name, i) in names.Select((name, i) => (name, i)))
        {
            _handler.RespondTo($"/repos/{name}", HttpStatusCode.OK, Repo(i + 1, name), TimeSpan.FromMilliseconds(50));
        }

        var result = await _provider.SearchTasksAsync("token", "bug", limit: 20);

        Assert.Equal(12, result.Count);
        Assert.InRange(_handler.MaxInFlight, 2, 5);
    }

    [Fact]
    public async Task SearchTasksAsync_FailedLookup_SurfacesThatFailureNotTheCancelledRest()
    {
        // The failure cancels the lookups still running; their cancellation must not replace the
        // status code consumers branch on.
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            total_count = 2,
            items = new[] { SearchItem(1, "owner/slow"), SearchItem(2, "owner/expired") }
        });
        _handler.RespondTo("/repos/owner/slow", HttpStatusCode.OK, Repo(1, "owner/slow"), TimeSpan.FromSeconds(30));
        _handler.RespondTo("/repos/owner/expired", HttpStatusCode.Unauthorized);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.SearchTasksAsync("token", "bug"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task SearchTasksAsync_Cancelled_ThrowsOperationCanceled()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { total_count = 1, items = new[] { SearchItem(1, "owner/a") } });
        _handler.RespondTo("/repos/owner/a", HttpStatusCode.OK, Repo(1, "owner/a"), TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _provider.SearchTasksAsync("token", "bug", cancellationToken: cts.Token));
    }

    [Theory]
    [MemberData(nameof(RateLimitResponses))]
    public async Task ValidateConnectionAsync_RateLimited_ThrowsTooManyRequests(RateLimitResponse limited)
    {
        _handler.EnqueueResponse(limited.Build);

        var ex = await Assert.ThrowsAsync<IntegrationRateLimitException>(() => _provider.ValidateConnectionAsync("token"));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    [Theory]
    [MemberData(nameof(RateLimitResponses))]
    public async Task GetProjectsAsync_RateLimitedPage_ThrowsTooManyRequests(RateLimitResponse limited)
    {
        // A later page, so following the Link header is covered as well as the first request.
        _handler.EnqueueResponseWithHeader(new[] { Repo(1, "owner/a") }, "Link", "<https://api.github.com/user/repos?page=2>; rel=\"next\"");
        _handler.EnqueueResponse(limited.Build);

        var ex = await Assert.ThrowsAsync<IntegrationRateLimitException>(() => _provider.GetProjectsAsync("token"));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    [Theory]
    [MemberData(nameof(RateLimitResponses))]
    public async Task GetTasksAsync_RateLimitedRepositoryLookup_ThrowsTooManyRequests(RateLimitResponse limited)
    {
        // Consumers read 403 as an expired token, and a sync hitting a rate limit is no reason to sign in again.
        _handler.EnqueueResponse(limited.Build);

        var ex = await Assert.ThrowsAsync<IntegrationRateLimitException>(() => _provider.GetTasksAsync("token", "owner/a"));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    [Theory]
    [MemberData(nameof(RateLimitResponses))]
    public async Task GetTasksAsync_RateLimitedIssuesPage_ThrowsTooManyRequests(RateLimitResponse limited)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Repo(1, "owner/a"));
        _handler.EnqueueResponse(limited.Build);

        var ex = await Assert.ThrowsAsync<IntegrationRateLimitException>(() => _provider.GetTasksAsync("token", "owner/a"));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    [Fact]
    public async Task GetTasksAsync_ForbiddenWithoutARateLimit_KeepsTheStatusCode()
    {
        _handler.EnqueueResponse(HttpStatusCode.Forbidden, new { message = "Resource protected by organization SAML enforcement." });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetTasksAsync("token", "owner/a"));

        Assert.IsNotType<IntegrationRateLimitException>(ex);
        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Fact]
    public async Task RateLimit_RetryAfterSeconds_RetryAtCountsFromTheResponseDate()
    {
        var sent = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        _handler.EnqueueResponse(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{}") };
            response.Headers.Add("Retry-After", "60");
            response.Headers.Date = sent;
            return response;
        });

        var ex = await Assert.ThrowsAsync<IntegrationRateLimitException>(() => _provider.GetProjectsAsync("token"));

        Assert.Equal(sent.AddSeconds(60), ex.RetryAt);
    }

    [Fact]
    public async Task RateLimit_RetryAfterDate_IsTheRetryAt()
    {
        var at = new DateTimeOffset(2026, 11, 7, 12, 5, 0, TimeSpan.Zero);
        _handler.EnqueueResponse(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{}") };
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(at);
            return response;
        });

        var ex = await Assert.ThrowsAsync<IntegrationRateLimitException>(() => _provider.GetProjectsAsync("token"));

        Assert.Equal(at, ex.RetryAt);
    }

    [Fact]
    public async Task RateLimit_NoRequestsRemaining_RetryAtIsTheReset()
    {
        _handler.EnqueueResponse(() =>
        {
            var response = RateLimited();
            response.Headers.Add("x-ratelimit-reset", "1793966400");
            return response;
        });

        var ex = await Assert.ThrowsAsync<IntegrationRateLimitException>(() => _provider.GetProjectsAsync("token"));

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1793966400), ex.RetryAt);
    }

    [Fact]
    public async Task RateLimit_SecondaryWithRequestsRemaining_RetryAtIsUnknown()
    {
        // The reset belongs to the primary window, which is not used up, so it says nothing here.
        _handler.EnqueueResponse(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("""{"message":"You have exceeded a secondary rate limit."}""")
            };
            response.Headers.Add("x-ratelimit-remaining", "4000");
            response.Headers.Add("x-ratelimit-reset", "1793966400");
            return response;
        });

        var ex = await Assert.ThrowsAsync<IntegrationRateLimitException>(() => _provider.GetProjectsAsync("token"));

        Assert.Null(ex.RetryAt);
    }

    public sealed record RateLimitResponse(string Name, Func<HttpResponseMessage> Build)
    {
        public override string ToString() => Name;
    }

    public static TheoryData<RateLimitResponse> RateLimitResponses =>
    [
        new RateLimitResponse("primary limit: no requests remaining", RateLimited),
        new RateLimitResponse("secondary limit: retry-after", () =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{}") };
            response.Headers.Add("Retry-After", "60");
            return response;
        }),
        new RateLimitResponse("secondary limit: only the message says so", () => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("""{"message":"You have exceeded a secondary rate limit."}""")
        }),
        new RateLimitResponse("429", () => new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{}") })
    ];

    private static HttpResponseMessage RateLimited()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{}") };
        response.Headers.Add("x-ratelimit-remaining", "0");
        return response;
    }

    private static object Repo(long id = 42, string fullName = "owner/repo") => new
    {
        id,
        name = fullName.Split('/')[1],
        full_name = fullName,
        html_url = $"https://github.com/{fullName}"
    };

    private static object SearchItem(int number, string fullName) => new
    {
        number,
        title = $"Issue {number}",
        state = "open",
        html_url = $"https://github.com/{fullName}/issues/{number}",
        repository_url = $"https://api.github.com/repos/{fullName}"
    };
}
