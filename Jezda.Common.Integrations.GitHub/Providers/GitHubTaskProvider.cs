using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Jezda.Common.Integrations.GitHub.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Jezda.Common.Integrations.GitHub.Providers;

public sealed class GitHubTaskProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<GitHubTaskProvider> logger) : IExternalTaskProvider
{
    public const string HttpClientName = "ExternalTaskProvider.GitHub";

    // GitHub allows 100 concurrent requests but advises making them serially; 5 keeps a search with
    // many repositories from reaching a secondary rate limit while a person still waits little.
    private const int MaxConcurrentLookups = 5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExternalProvider Provider => ExternalProvider.GitHub;

    public async Task<bool> ValidateConnectionAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        if (!AccessTokenRules.CanGoInHeader(accessToken))
        {
            logger.LogWarning("GitHub token has characters that cannot go in a header, so it was not sent");
            return false;
        }

        using var client = CreateClient(accessToken);
        using var response = await client.GetAsync("user", cancellationToken);

        // GitHub answers a bad token with 401. A 403 can refuse a valid one: a rate limit, a lockout
        // after failed logins from the same address, an IP allow list.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogWarning("GitHub rejected the token with {StatusCode}", (int)response.StatusCode);
            return false;
        }

        response.EnsureSuccessStatusCode();

        return true;
    }

    public async Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        var repos = await GetAllPagesAsync<GitHubRepository>(client, "user/repos?per_page=100", cancellationToken);

        return [.. repos.Select(r => new ExternalProjectDto
        {
            Id = DurableId(r),
            Path = r.FullName,
            Name = r.Name,
            Description = r.Description,
            Url = r.HtmlUrl,
            Provider = ExternalProvider.GitHub,
            CreatedAt = r.CreatedAt,
            UpdatedAt = LastChange(r),
            IsArchived = r.Archived,
            IsPrivate = r.Private
        })];
    }

    /// <summary>
    /// Lists a repository's issues.
    /// </summary>
    /// <remarks>
    /// The repository is looked up first so every task carries the durable id whichever form of
    /// <paramref name="projectId"/> was passed, because the issues payload has no repository id.
    /// Looking it up by id also survives a rename: the old <c>owner/repo</c> answers with a redirect,
    /// on which .NET drops the Authorization header, so a private repository would fail with 404.
    /// </remarks>
    public async Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(string accessToken, string projectId, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        // Blank would otherwise address GitHub's listing of every public repository.
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        using var client = CreateClient(accessToken);

        // Only a path has a slash, so the two forms cannot be confused.
        var repository = await GetRepositoryAsync(
            client,
            projectId.Contains('/') ? $"repos/{projectId}" : $"repositories/{Uri.EscapeDataString(projectId)}",
            cancellationToken);

        var allIssues = await GetAllPagesAsync<GitHubIssue>(
            client, $"repos/{repository.FullName}/issues?state=all&per_page=100", cancellationToken);

        return [.. allIssues
            .Where(i => i.PullRequest is null) // GitHub's issues endpoint returns PRs too — keep only real issues
            .Select(i => new ExternalTaskDto
        {
            Id = i.Number.ToString(),
            Title = i.Title,
            Status = i.State,
            Url = i.HtmlUrl,
            ProjectId = DurableId(repository),
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
    /// <b>One extra request per distinct repository in the result.</b> A search item names its
    /// repository only through <c>repository_url</c> and carries no repository id, but
    /// <c>ProjectId</c> must equal what <see cref="GetProjectsAsync"/> reports as
    /// <c>ExternalProjectDto.Id</c>: consumers key stored rows on that value, so anything else would
    /// store a second row for a task they already have. These count against the core rate limit, not
    /// the search one, and at most five run at once. An item whose repository answers 404, 451 or a
    /// 403 that is not a rate limit (a SAML requirement, an IP allow list, a blocked repository)
    /// cannot be bound to a project, so it is dropped and logged.
    /// </para>
    /// </remarks>
    /// <exception cref="HttpRequestException">
    /// The search or a lookup failed. A rate limit always has status 429, even where GitHub answered
    /// 403, so it does not read as a rejected token; a 403 from the search itself that is not a rate
    /// limit stays 403.
    /// </exception>
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

        // `is:issue` is applied server-side by the search index, so `per_page` already pages a
        // pull-request-free result set and asking for headroom would only transfer rows to discard
        // — search items carry the full issue payload (body, labels, assignees), several KB each.
        // The filter below stays as a cheap guard, not as the thing `per_page` is sized around.
        // 100 is GitHub's per-page maximum.
        var perPage = Math.Min(limit, 100);
        var query = Uri.EscapeDataString($"{searchTerm.Trim()} is:issue involves:@me");

        using var searchResponse = await client.GetAsync($"search/issues?q={query}&per_page={perPage}", cancellationToken);
        await ThrowIfRateLimitedAsync(searchResponse, cancellationToken);
        searchResponse.EnsureSuccessStatusCode();

        var response = await searchResponse.Content.ReadFromJsonAsync<GitHubIssueSearchResponse>(JsonOptions, cancellationToken);

        var items = (response?.Items ?? [])
            .Where(i => i.PullRequest is null)
            .Take(limit)
            .Select(i => (Issue: i, FullName: ToFullName(i.RepositoryUrl)))
            .ToList();

        var repositories = await FindRepositoriesAsync(client, items.Select(i => i.FullName), cancellationToken);

        return [.. items
            .Where(i => repositories.ContainsKey(i.FullName))
            .Select(i => new ExternalTaskDto
        {
            Id = i.Issue.Number.ToString(),
            Title = i.Issue.Title,
            Status = i.Issue.State,
            Url = i.Issue.HtmlUrl,
            ProjectId = DurableId(repositories[i.FullName]),
            Provider = ExternalProvider.GitHub
        })];
    }

    /// <summary>
    /// Looks up each named repository once, a few at a time because a person is waiting on the search.
    /// A repository GitHub refuses for its own reasons is left out, so it does not fail the rest. Any
    /// other failure propagates and cancels the lookups still running, so an expired token still
    /// surfaces as one.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, GitHubRepository>> FindRepositoriesAsync(
        HttpClient client,
        IEnumerable<string> fullNames,
        CancellationToken cancellationToken)
    {
        var found = new ConcurrentDictionary<string, GitHubRepository>(StringComparer.Ordinal);
        var names = fullNames.Where(name => name.Length > 0).Distinct(StringComparer.Ordinal);
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentLookups, CancellationToken = cancellationToken };

        await Parallel.ForEachAsync(names, options, async (name, token) =>
        {
            var url = $"repos/{name}";
            using var response = await client.GetAsync(url, token);
            await ThrowIfRateLimitedAsync(response, token);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.UnavailableForLegalReasons)
            {
                logger.LogWarning(
                    "GitHub search returned an issue in {Repository}, which answers {StatusCode}", name, (int)response.StatusCode);
                return;
            }

            response.EnsureSuccessStatusCode();
            found[name] = await ReadRepositoryAsync(response, url, token);
        });

        return found;
    }

    private static async Task<GitHubRepository> GetRepositoryAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await ReadRepositoryAsync(response, url, cancellationToken);
    }

    private static async Task<GitHubRepository> ReadRepositoryAsync(HttpResponseMessage response, string url, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<GitHubRepository>(JsonOptions, cancellationToken)
        ?? throw new HttpRequestException($"GitHub returned an empty repository for {url}");

    /// <summary>
    /// Throws with status 429 when GitHub reports a rate limit, which it can do with 403 as well.
    /// Consumers read a 403 as an expired token, and a rate limit is no reason to sign in again.
    /// </summary>
    private static async Task ThrowIfRateLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests
            || (response.StatusCode == HttpStatusCode.Forbidden && await IsRateLimitRefusalAsync(response, cancellationToken)))
        {
            throw new HttpRequestException(
                $"GitHub rate limit exceeded (answered {(int)response.StatusCode})",
                inner: null,
                statusCode: HttpStatusCode.TooManyRequests);
        }
    }

    /// <summary>
    /// Recognises a rate limit on a 403. A primary limit has <c>x-ratelimit-remaining: 0</c>. A
    /// secondary one may have <c>retry-after</c>, but only its message is certain.
    /// </summary>
    private static async Task<bool> IsRateLimitRefusalAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Headers.RetryAfter is not null
            || (response.Headers.TryGetValues("x-ratelimit-remaining", out var remaining) && remaining.FirstOrDefault() == "0"))
        {
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return body.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
    }

    private static string DurableId(GitHubRepository repository) =>
        repository.Id.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The later of the last push and the last metadata change, since either alone misses the other
    /// kind of activity. <c>pushed_at</c> is null on a repository nothing was ever pushed to.
    /// </summary>
    private static DateTimeOffset? LastChange(GitHubRepository repository) =>
        repository.PushedAt > repository.UpdatedAt || repository.UpdatedAt is null
            ? repository.PushedAt
            : repository.UpdatedAt;

    /// <summary>
    /// Follows <c>Link: rel="next"</c> until the last page.
    /// </summary>
    private static async Task<List<T>> GetAllPagesAsync<T>(HttpClient client, string firstPageUrl, CancellationToken cancellationToken)
    {
        var all = new List<T>();
        string? url = firstPageUrl;

        while (url is not null)
        {
            using var response = await client.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            all.AddRange(await response.Content.ReadFromJsonAsync<List<T>>(JsonOptions, cancellationToken) ?? []);

            url = GetNextPageUrl(response);
        }

        return all;
    }

    /// <summary>
    /// Turns <c>https://api.github.com/repos/{owner}/{repo}</c> into <c>{owner}/{repo}</c>.
    /// </summary>
    private static string ToFullName(string repositoryUrl)
    {
        // Null is the only case the length check below cannot absorb — "" and whitespace both split
        // into a single segment and fall through to string.Empty on their own.
        if (repositoryUrl is null)
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
