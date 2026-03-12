using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Jezda.Common.Integrations.Monday.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Jezda.Common.Integrations.Monday.Providers;

public sealed class MondayTaskProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<MondayTaskProvider> logger) : IExternalTaskProvider
{
    public const string HttpClientName = "ExternalTaskProvider.Monday";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExternalProvider Provider => ExternalProvider.Monday;

    public async Task<bool> ValidateConnectionAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        try
        {
            var response = await ExecuteGraphQlAsync<MondayMeData>(
                client, "{ me { id } }", cancellationToken);

            return response?.Data?.Me is not null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Monday.com connection validation failed");
            return false;
        }
    }

    public async Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        var response = await ExecuteGraphQlAsync<MondayBoardsData>(
            client, "{ boards(limit:100) { id name description } }", cancellationToken);

        var boards = response?.Data?.Boards ?? [];

        return [.. boards.Select(b => new ExternalProjectDto
        {
            Id = b.Id,
            Name = b.Name,
            Description = b.Description,
            Url = $"https://monday.com/boards/{b.Id}",
            Provider = ExternalProvider.Monday
        })];
    }

    public async Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(string accessToken, string projectId, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        var query = $$"""{ boards(ids:{{projectId}}) { items_page { items { id name state } } } }""";
        var response = await ExecuteGraphQlAsync<MondayBoardItemsData>(
            client, query, cancellationToken);

        var items = response?.Data?.Boards?.FirstOrDefault()?.ItemsPage?.Items ?? [];

        return [.. items.Select(i => new ExternalTaskDto
        {
            Id = i.Id,
            Title = i.Name,
            Status = i.State,
            Url = $"https://monday.com/boards/{projectId}/pulses/{i.Id}",
            ProjectId = projectId,
            Provider = ExternalProvider.Monday
        })];
    }

    private HttpClient CreateClient(string accessToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static async Task<MondayGraphQlResponse<T>?> ExecuteGraphQlAsync<T>(
        HttpClient client, string query, CancellationToken cancellationToken)
    {
        var request = new MondayGraphQlRequest { Query = query };
        var response = await client.PostAsJsonAsync("", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MondayGraphQlResponse<T>>(JsonOptions, cancellationToken);
    }
}
