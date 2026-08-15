using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Jezda.Common.Integrations.GitHub.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Jezda.Common.Integrations.GitHub.Providers;

public sealed class GitHubTaskProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<GitHubTaskProvider> logger) : IExternalTaskProvider
{
    public const string HttpClientName = "ExternalTaskProvider.GitHub";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExternalProvider Provider => ExternalProvider.GitHub;

    public async Task<bool> ValidateConnectionAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        try
        {
            var response = await client.GetAsync("user", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GitHub connection validation failed");
            return false;
        }
    }

    public async Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        var repos = await client.GetFromJsonAsync<List<GitHubRepository>>("user/repos", JsonOptions, cancellationToken)
                    ?? [];

        return [.. repos.Select(r => new ExternalProjectDto
        {
            Id = r.FullName,
            Name = r.Name,
            Description = r.Description,
            Url = r.HtmlUrl,
            Provider = ExternalProvider.GitHub
        })];
    }

    public async Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(string accessToken, string projectId, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        var allIssues = new List<GitHubIssue>();
        string? url = $"repos/{projectId}/issues?state=all&per_page=100";

        while (url is not null)
        {
            var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var page = await response.Content.ReadFromJsonAsync<List<GitHubIssue>>(JsonOptions, cancellationToken) ?? [];
            allIssues.AddRange(page);

            url = GetNextPageUrl(response);
        }

        return [.. allIssues.Select(i => new ExternalTaskDto
        {
            Id = i.Number.ToString(),
            Title = i.Title,
            Status = i.State,
            Url = i.HtmlUrl,
            ProjectId = projectId,
            Provider = ExternalProvider.GitHub
        })];
    }

    /// <summary>
    /// Searches issues through GitHub's search API — one request instead of the base
    /// implementation's one-per-repository fan-out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scope is <c>involves:@me</c>, which is narrower than <see cref="GetTasksAsync"/>.</b> It
    /// finds issues the token's user opened, was assigned, was mentioned in, or commented on — not
    /// every issue in every repository they can read, which is what a <c>user/repos</c> fan-out
    /// covers. GitHub's search requires a scoping qualifier (an unscoped term searches all of
    /// GitHub), and the alternative — one <c>repo:</c> qualifier per repository, built from
    /// <c>user/repos</c> — does not fit inside the 256-character query limit for anyone with a
    /// realistic number of repositories. For the case this exists to serve, picking a task to log
    /// time against, "issues I am involved in" is the right set; a task nobody has touched yet is
    /// the known gap.
    /// </para>
    /// <para>
    /// <b>Rate limits are separate and much tighter</b> — the search API allows 30 requests per
    /// minute for an authenticated user, against 5,000 per hour for the core API. Callers driving
    /// this from a text field must debounce.
    /// </para>
    /// <para>
    /// <c>ProjectId</c> is derived from each item's <c>repository_url</c> rather than a parameter,
    /// and must equal the <c>owner/repo</c> full name <see cref="GetProjectsAsync"/> reports as
    /// <c>ExternalProjectDto.Id</c> — consumers key stored rows on that value, so a different
    /// spelling here would store a second row for a task they already have.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<ExternalTaskDto>> SearchTasksAsync(
        string accessToken,
        string searchTerm,
        int limit = 20,
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || limit <= 0)
        {
            return [];
        }

        using var client = CreateClient(accessToken);

        // GitHub counts `per_page` before `is:issue` filtering is visible to us, and we drop pull
        // requests below, so ask for headroom and trim after. 100 is GitHub's per-page maximum.
        var perPage = Math.Min(limit * 2, 100);
        var query = Uri.EscapeDataString($"{searchTerm.Trim()} is:issue involves:@me");

        var response = await client.GetFromJsonAsync<GitHubIssueSearchResponse>(
            $"search/issues?q={query}&per_page={perPage}", JsonOptions, cancellationToken);

        var items = (response?.Items ?? [])
            .Where(i => i.PullRequest is null)
            .Take(limit);

        return [.. items.Select(i => new ExternalTaskDto
        {
            Id = i.Number.ToString(),
            Title = i.Title,
            Status = i.State,
            Url = i.HtmlUrl,
            ProjectId = ToFullName(i.RepositoryUrl),
            Provider = ExternalProvider.GitHub
        })];
    }

    /// <summary>
    /// Turns <c>https://api.github.com/repos/{owner}/{repo}</c> into <c>{owner}/{repo}</c>, the same
    /// value <see cref="GetProjectsAsync"/> reports from <c>GitHubRepository.FullName</c>.
    /// </summary>
    private static string ToFullName(string repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return string.Empty;
        }

        var segments = repositoryUrl.TrimEnd('/').Split('/');
        return segments.Length >= 2
            ? $"{segments[^2]}/{segments[^1]}"
            : string.Empty;
    }

    private static string? GetNextPageUrl(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var linkValues))
            return null;

        var link = linkValues.FirstOrDefault();
        if (link is null) return null;

        foreach (var part in link.Split(','))
        {
            var trimmed = part.Trim();
            if (!trimmed.Contains("rel=\"next\"")) continue;

            var urlPart = trimmed.Split(';')[0].Trim();
            return urlPart.TrimStart('<').TrimEnd('>');
        }

        return null;
    }

    private HttpClient CreateClient(string accessToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
