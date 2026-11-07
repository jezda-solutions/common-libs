using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Jezda.Common.Integrations.ClickUp.Models;
using Jezda.Common.Integrations.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Jezda.Common.Integrations.ClickUp.Providers;

public sealed class ClickUpTaskProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<ClickUpTaskProvider> logger) : IExternalTaskProvider
{
    public const string HttpClientName = "ExternalTaskProvider.ClickUp";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExternalProvider Provider => ExternalProvider.ClickUp;

    public async Task<bool> ValidateConnectionAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        if (!AccessTokenRules.CanGoInHeader(accessToken))
        {
            logger.LogWarning("ClickUp token has characters that cannot go in a header, so it was not sent");
            return false;
        }

        using var client = CreateClient(accessToken);
        using var response = await client.GetAsync("team", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogWarning("ClickUp rejected the token with {StatusCode}", (int)response.StatusCode);
            return false;
        }

        response.EnsureSuccessStatusCode();

        return true;
    }

    public async Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        var teamsResponse = await client.GetFromUtf8JsonAsync<ClickUpTeamsResponse>(
            "team", JsonOptions, cancellationToken);

        var teams = teamsResponse?.Teams ?? [];

        return [.. teams.Select(t => new ExternalProjectDto
        {
            Id = t.Id,
            Path = t.Id, // A team id is ClickUp's only identifier for it, and it does not change.
            Name = t.Name,
            Description = null,
            Url = $"https://app.clickup.com/{t.Id}",
            Provider = ExternalProvider.ClickUp
        })];
    }

    public async Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(string accessToken, string projectId, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        var tasksResponse = await client.GetFromUtf8JsonAsync<ClickUpTasksResponse>(
            $"team/{projectId}/task", JsonOptions, cancellationToken);

        var tasks = tasksResponse?.Tasks ?? [];

        return [.. tasks.Select(t => new ExternalTaskDto
        {
            Id = t.Id,
            Title = t.Name,
            Status = t.Status?.Status,
            Url = t.Url,
            ProjectId = projectId,
            Provider = ExternalProvider.ClickUp
        })];
    }

    private HttpClient CreateClient(string accessToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
