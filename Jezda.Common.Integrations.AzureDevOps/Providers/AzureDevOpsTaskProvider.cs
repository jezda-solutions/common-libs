using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Jezda.Common.Integrations.AzureDevOps.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Jezda.Common.Integrations.AzureDevOps.Providers;

public sealed class AzureDevOpsTaskProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<AzureDevOpsTaskProvider> logger) : IExternalTaskProvider
{
    public const string HttpClientName = "ExternalTaskProvider.AzureDevOps";
    private const string ApiVersion = "7.1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExternalProvider Provider => ExternalProvider.AzureDevOps;

    public async Task<bool> ValidateConnectionAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));

        using var client = CreateClient(accessToken, baseUrl);

        try
        {
            var response = await client.GetAsync($"_apis/projects?$top=1&api-version={ApiVersion}", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Azure DevOps connection validation failed");
            return false;
        }
    }

    public async Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));

        using var client = CreateClient(accessToken, baseUrl);

        var response = await client.GetFromJsonAsync<AdoProjectListResponse>(
            $"_apis/projects?api-version={ApiVersion}", JsonOptions, cancellationToken);

        return (response?.Value ?? []).Select(p => new ExternalProjectDto
        {
            Id = p.Name,
            Name = p.Name,
            Description = p.Description,
            Url = p.Url,
            Provider = ExternalProvider.AzureDevOps
        }).ToList();
    }

    public async Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(
        string projectId, 
        string accessToken, 
        string? baseUrl = null, 
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));

        using var client = CreateClient(accessToken, baseUrl);

        // Execute WIQL query to get work item IDs
        var wiqlRequest = new AdoWiqlRequest
        {
            Query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{projectId}' AND [System.State] <> 'Removed' ORDER BY [System.Id] DESC"
        };

        var wiqlResponse = await client.PostAsJsonAsync(
            $"{Uri.EscapeDataString(projectId)}/_apis/wit/wiql?api-version={ApiVersion}", wiqlRequest, JsonOptions, cancellationToken);
        wiqlResponse.EnsureSuccessStatusCode();

        var wiqlResult = await wiqlResponse.Content.ReadFromJsonAsync<AdoWiqlResponse>(JsonOptions, cancellationToken);

        if (wiqlResult?.WorkItems is not { Count: > 0 })
        {
            return [];
        }

        // Batch fetch work item details
        var allWorkItems = new List<AdoWorkItem>();

        foreach (var batch in wiqlResult.WorkItems.Chunk(200))
        {
            var idsString = string.Join(",", batch.Select(wi => wi.Id));
            var detailsUrl = $"_apis/wit/workitems?ids={idsString}&api-version={ApiVersion}";
            var detailsResponse = await client.GetFromJsonAsync<AdoWorkItemListResponse>(detailsUrl, JsonOptions, cancellationToken);

            if (detailsResponse?.Value != null)
            {
                allWorkItems.AddRange(detailsResponse.Value);
            }
        }

        return [.. allWorkItems.Select(wi => new ExternalTaskDto
        {
            Id = wi.Id.ToString(),
            Title = wi.Title,
            Status = wi.State,
            Url = wi.Url,
            ProjectId = projectId,
            Provider = ExternalProvider.AzureDevOps
        })];
    }

    private HttpClient CreateClient(string accessToken, string baseUrl)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");

        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{accessToken}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

        return client;
    }
}
