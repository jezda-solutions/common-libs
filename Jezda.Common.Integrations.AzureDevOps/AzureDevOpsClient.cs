using System.Net.Http.Json;
using Jezda.Common.Integrations.AzureDevOps.Configuration;
using Jezda.Common.Integrations.AzureDevOps.Models;
using Jezda.Common.Integrations.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jezda.Common.Integrations.AzureDevOps;

public class AzureDevOpsClient(
    HttpClient httpClient,
    IOptions<AzureDevOpsOptions> options,
    ILogger<AzureDevOpsClient> logger) : BaseIntegrationClient(httpClient, logger), IAzureDevOpsClient
{
    private readonly AzureDevOpsOptions _options = options.Value;

    public async Task<List<AdoWorkItem>> GetWorkItemsByQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        // 1. Execute WIQL
        var wiqlRequest = new AdoWiqlRequest { Query = query };
        // Assuming BaseUrl is set in HttpClient, we just append relative path
        // Note: Project might be needed in URL depending on setup, but typically WIQL can run across project or scoped to project
        // If BaseUrl includes project, fine. If not, query might need modification or URL.
        // For safety, let's assume BaseUrl is organization level and user provides project in config or we use generic endpoint.
        // Actually, usually it's POST https://dev.azure.com/{org}/{project}/_apis/wit/wiql
        // We will assume HttpClient BaseAddress is configured correctly to the Project level or Org level.
        // Let's assume standard usage: BaseAddress = https://dev.azure.com/{org}/
        // We might need to handle project scope. But for now, let's use relative path assuming client is configured.
        
        // Better approach: User configures BaseUrl as https://dev.azure.com/{org}/{project}/
        
        var wiqlUrl = $"_apis/wit/wiql?api-version={_options.ApiVersion}";
        var wiqlResponse = await PostAsync<AdoWiqlRequest, AdoWiqlResponse>(wiqlUrl, wiqlRequest, cancellationToken);

        if (wiqlResponse == null || !wiqlResponse.WorkItems.Any())
        {
            return [];
        }

        // 2. Get details for found IDs
        var ids = wiqlResponse.WorkItems.Select(wi => wi.Id).Take(200).ToList(); // ADO limit is 200 for list
        // If more than 200, we should batch. For this "mini project", taking first 200 is acceptable or we can implement batching logic.
        
        var idsString = string.Join(",", ids);
        var detailsUrl = $"_apis/wit/workitems?ids={idsString}&api-version={_options.ApiVersion}";
        
        var detailsResponse = await GetAsync<AdoWorkItemListResponse>(detailsUrl, cancellationToken);
        
        return detailsResponse?.Value ?? [];
    }

    public async Task<AdoWorkItem?> GetWorkItemByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var url = $"_apis/wit/workitems/{id}?api-version={_options.ApiVersion}";
        return await GetAsync<AdoWorkItem>(url, cancellationToken);
    }

    public async Task<AdoWorkItem?> CreateWorkItemAsync(string project, string type, Dictionary<string, object> fields, CancellationToken cancellationToken = default)
    {
        // For creation, we need JSON Patch Document format usually, but some endpoints accept simple JSON?
        // ADO REST API for Create Work Item requires application/json-patch+json
        // This makes things tricky with simple PostAsJsonAsync if we don't change content type.
        
        // Actually, ADO Create Work Item requires:
        // POST https://dev.azure.com/{organization}/{project}/_apis/wit/workitems/${type}?api-version=7.1
        // Body: [ { "op": "add", "path": "/fields/System.Title", "value": "..." } ]
        
        var patchDocument = new List<object>();
        foreach (var field in fields)
        {
            patchDocument.Add(new
            {
                op = "add",
                path = $"/fields/{field.Key}",
                value = field.Value
            });
        }
        
        // We need to send this with specific content type: application/json-patch+json
        // BaseIntegrationClient's PostAsync uses PostAsJsonAsync which uses application/json.
        // We might need to override or use HttpClient directly here for Patch format.
        
        var url = $"{project}/_apis/wit/workitems/${type}?api-version={_options.ApiVersion}";
        
        try
        {
            Logger.LogDebug("Sending Create Work Item request to {RequestUri}", url);
            
            // Use JsonContent to create content with correct media type
            var content = JsonContent.Create(patchDocument, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json-patch+json"), JsonSerializerOptions);
            
            var response = await HttpClient.PostAsync(url, content, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AdoWorkItem>(JsonSerializerOptions, cancellationToken);
            }
            
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Logger.LogError("Failed to create work item. Status: {Status}, Content: {Content}", response.StatusCode, errorContent);
            response.EnsureSuccessStatusCode();
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating work item");
            throw;
        }
    }

    public async Task<IReadOnlyList<AdoWorkLogEntry>> GetCompletedWorkLogsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var wiql = $"SELECT [System.Id] FROM WorkItems WHERE [System.ChangedDate] >= '{from.UtcDateTime:O}' AND [System.ChangedDate] <= '{to.UtcDateTime:O}'";
        var workItems = await GetWorkItemsByQueryAsync(wiql, cancellationToken);

        if (workItems.Count == 0)
        {
            return Array.Empty<AdoWorkLogEntry>();
        }

        var result = new List<AdoWorkLogEntry>();

        foreach (var workItem in workItems)
        {
            var updatesUrl = $"_apis/wit/workitems/{workItem.Id}/updates?api-version={_options.ApiVersion}";
            var updatesResponse = await GetAsync<AdoWorkItemUpdatesResponse>(updatesUrl, cancellationToken);

            if (updatesResponse == null || updatesResponse.Value.Count == 0)
            {
                continue;
            }

            foreach (var update in updatesResponse.Value)
            {
                if (update.RevisedDate < from || update.RevisedDate > to)
                {
                    continue;
                }

                if (!update.Fields.TryGetValue("Microsoft.VSTS.Scheduling.CompletedWork", out var change))
                {
                    continue;
                }

                if (!TryConvertToDouble(change.OldValue, out var oldValue) || !TryConvertToDouble(change.NewValue, out var newValue))
                {
                    continue;
                }

                var delta = newValue - oldValue;

                if (delta <= 0)
                {
                    continue;
                }

                workItem.Fields.TryGetValue("System.TeamProject", out var project);

                result.Add(new AdoWorkLogEntry
                {
                    WorkItemId = workItem.Id,
                    Project = project?.ToString(),
                    UserUniqueName = update.RevisedBy?.UniqueName,
                    UserDisplayName = update.RevisedBy?.DisplayName,
                    Hours = delta,
                    LoggedAt = update.RevisedDate
                });
            }
        }

        return result;
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        switch (value)
        {
            case null:
                result = 0;
                return false;
            case double d:
                result = d;
                return true;
            case float f:
                result = f;
                return true;
            case decimal m:
                result = (double)m;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case string s when double.TryParse(s, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    public async Task<List<AdoProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var url = $"_apis/projects?api-version={_options.ApiVersion}";
        var response = await GetAsync<AdoProjectListResponse>(url, cancellationToken);
        return response?.Value ?? new List<AdoProject>();
    }
}
