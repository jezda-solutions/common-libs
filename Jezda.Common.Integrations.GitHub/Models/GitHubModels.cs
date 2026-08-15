using System.Text.Json.Serialization;

namespace Jezda.Common.Integrations.GitHub.Models;

public sealed class GitHubRepository
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("private")]
    public bool Private { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("owner")]
    public GitHubUser? Owner { get; set; }
}

public sealed class GitHubIssue
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("user")]
    public GitHubUser? User { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// One page of <c>GET /search/issues</c>. The items are ordinary issues plus a
/// <c>repository_url</c> the plain issues endpoint does not need to send, because there the
/// repository was in the request path.
/// </summary>
public sealed class GitHubIssueSearchResponse
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("items")]
    public List<GitHubSearchIssue> Items { get; set; } = [];
}

public sealed class GitHubSearchIssue
{
    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    /// <summary>
    /// API URL of the owning repository, e.g. <c>https://api.github.com/repos/jezda-solutions/serp</c>.
    /// Its last two segments are the <c>owner/repo</c> full name.
    /// </summary>
    [JsonPropertyName("repository_url")]
    public string RepositoryUrl { get; set; } = string.Empty;

    // Same meaning as on GitHubIssue: non-null marks the item as a pull request.
    [JsonPropertyName("pull_request")]
    public GitHubPullRequestRef? PullRequest { get; set; }
}

/// <summary>
/// Present on a search item only when it is a pull request. GitHub's issue search returns PRs
/// alongside issues, and a non-null value is what distinguishes them.
/// </summary>
public sealed class GitHubPullRequestRef
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class GitHubUser
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;
}
