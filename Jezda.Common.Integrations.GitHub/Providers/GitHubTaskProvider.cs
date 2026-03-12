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

    public async Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(string projectId, string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        var url = $"repos/{projectId}/issues?state=open";
        var issues = await client.GetFromJsonAsync<List<GitHubIssue>>(url, JsonOptions, cancellationToken)
                     ?? [];

        return [.. issues.Select(i => new ExternalTaskDto
        {
            Id = i.Number.ToString(),
            Title = i.Title,
            Status = i.State,
            Url = i.HtmlUrl,
            ProjectId = projectId,
            Provider = ExternalProvider.GitHub
        })];
    }

    private HttpClient CreateClient(string accessToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
