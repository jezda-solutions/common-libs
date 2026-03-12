using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Jezda.Common.Integrations.ClickUp.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
        using var client = CreateClient(accessToken);

        try
        {
            var response = await client.GetAsync("team", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ClickUp connection validation failed");
            return false;
        }
    }

    public async Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        var teamsResponse = await client.GetFromJsonAsync<ClickUpTeamsResponse>(
            "team", JsonOptions, cancellationToken);

        var teams = teamsResponse?.Teams ?? [];

        return [.. teams.Select(t => new ExternalProjectDto
        {
            Id = t.Id,
            Name = t.Name,
            Description = null,
            Url = $"https://app.clickup.com/{t.Id}",
            Provider = ExternalProvider.ClickUp
        })];
    }

    public async Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(string accessToken, string projectId, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        var tasksResponse = await client.GetFromJsonAsync<ClickUpTasksResponse>(
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
