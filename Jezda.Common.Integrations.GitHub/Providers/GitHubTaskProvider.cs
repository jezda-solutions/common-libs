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

        return [.. allIssues
            .Where(i => i.PullRequest is null) // GitHub's issues endpoint returns PRs too — keep only real issues
            .Select(i => new ExternalTaskDto
        {
            Id = i.Number.ToString(),
            Title = i.Title,
            Status = i.State,
            Url = i.HtmlUrl,
            ProjectId = projectId,
            Provider = ExternalProvider.GitHub
        })];
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
