using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Jezda.Common.Integrations.AzureDevOps.Configuration;
using Jezda.Common.Integrations.AzureDevOps.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Jezda.Common.Integrations.AzureDevOps.Providers;

public sealed class AzureDevOpsTaskProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<AzureDevOpsTaskProvider> logger,
    IOptions<AzureDevOpsOptions> options) : IExternalTaskProvider
{
    public const string HttpClientName = "ExternalTaskProvider.AzureDevOps";
    private readonly string _apiVersion = options.Value.ApiVersion;

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
            var response = await client.GetAsync($"_apis/projects?$top=1&api-version={_apiVersion}", cancellationToken);
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
            $"_apis/projects?api-version={_apiVersion}", JsonOptions, cancellationToken);

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
        string accessToken,
        string projectId,
        string? baseUrl = null, 
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));

        using var client = CreateClient(accessToken, baseUrl);

        // Execute WIQL query to get work item IDs
        var sanitizedProjectId = projectId.Replace("'", "''");
        var wiqlRequest = new AdoWiqlRequest
        {
            Query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{sanitizedProjectId}' AND [System.State] <> 'Removed' ORDER BY [System.Id] DESC"
        };

        var wiqlResponse = await client.PostAsJsonAsync(
            $"{Uri.EscapeDataString(projectId)}/_apis/wit/wiql?api-version={_apiVersion}", wiqlRequest, JsonOptions, cancellationToken);
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
            var detailsUrl = $"_apis/wit/workitems?ids={idsString}&api-version={_apiVersion}";
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

    /// <summary>
    /// Searches work items by title across the whole organisation — one WIQL query plus one details
    /// batch, instead of the base implementation's query-per-project fan-out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The WIQL omits <c>[System.TeamProject]</c> and the URL omits the project segment, which is
    /// what makes this organisation-wide. Consequently the project cannot come from a parameter —
    /// it is read back per work item from <c>System.TeamProject</c>, and matches the value
    /// <see cref="GetProjectsAsync"/> reports as <c>ExternalProjectDto.Id</c> (the project
    /// <i>name</i>, not its GUID). Consumers key stored rows on that, so the two must agree.
    /// </para>
    /// <para>
    /// <c>CONTAINS</c> is a substring match on the title only, and is what Azure DevOps can index;
    /// <c>CONTAINS WORDS</c> would be full-text but requires the search extension to be installed.
    /// Results come back newest-changed first, which is the useful order for someone looking for
    /// what they were working on.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<ExternalTaskDto>> SearchTasksAsync(
        string accessToken,
        string searchTerm,
        int limit = 20,
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));

        if (string.IsNullOrWhiteSpace(searchTerm) || limit <= 0)
        {
            return [];
        }

        using var client = CreateClient(accessToken, baseUrl);

        // Same escaping as GetTasksAsync: WIQL string literals are single-quoted, and a quote in
        // the user's text would otherwise end the literal.
        var sanitizedTerm = searchTerm.Trim().Replace("'", "''");
        var wiqlRequest = new AdoWiqlRequest
        {
            Query = $"SELECT [System.Id] FROM WorkItems WHERE [System.Title] CONTAINS '{sanitizedTerm}' AND [System.State] <> 'Removed' ORDER BY [System.ChangedDate] DESC"
        };

        var wiqlResponse = await client.PostAsJsonAsync(
            $"_apis/wit/wiql?api-version={_apiVersion}", wiqlRequest, JsonOptions, cancellationToken);
        wiqlResponse.EnsureSuccessStatusCode();

        var wiqlResult = await wiqlResponse.Content.ReadFromJsonAsync<AdoWiqlResponse>(JsonOptions, cancellationToken);

        if (wiqlResult?.WorkItems is not { Count: > 0 })
        {
            return [];
        }

        // Trim to `limit` before fetching details: WIQL returns ids only, so the expensive call is
        // the one below and there is no reason to hydrate rows the caller will discard.
        var ids = wiqlResult.WorkItems.Take(limit).Select(wi => wi.Id);
        var idsString = string.Join(",", ids);

        var detailsResponse = await client.GetFromJsonAsync<AdoWorkItemListResponse>(
            $"_apis/wit/workitems?ids={idsString}&api-version={_apiVersion}", JsonOptions, cancellationToken);

        return [.. (detailsResponse?.Value ?? []).Select(wi => new ExternalTaskDto
        {
            Id = wi.Id.ToString(),
            Title = wi.Title,
            Status = wi.State,
            Url = wi.Url,
            ProjectId = wi.TeamProject,
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
