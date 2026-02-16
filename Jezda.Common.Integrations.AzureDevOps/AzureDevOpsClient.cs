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
        var wiqlRequest = new AdoWiqlRequest { Query = query };
        var wiqlUrl = $"_apis/wit/wiql?api-version={_options.ApiVersion}";
        var wiqlResponse = await PostAsync<AdoWiqlRequest, AdoWiqlResponse>(wiqlUrl, wiqlRequest, cancellationToken);

        if (wiqlResponse == null || !wiqlResponse.WorkItems.Any())
        {
            return [];
        }

        var workItemIds = wiqlResponse.WorkItems.Select(wi => wi.Id).ToList();
        var allWorkItems = new List<AdoWorkItem>();

        foreach (var batch in workItemIds.Chunk(200))
        {
            var idsString = string.Join(",", batch);
            var detailsUrl = $"_apis/wit/workitems?ids={idsString}&api-version={_options.ApiVersion}";
            var detailsResponse = await GetAsync<AdoWorkItemListResponse>(detailsUrl, cancellationToken);

            if (detailsResponse?.Value != null)
            {
                allWorkItems.AddRange(detailsResponse.Value);
            }
        }

        return allWorkItems;
    }

    public async Task<AdoWorkItem?> GetWorkItemByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var url = $"_apis/wit/workitems/{id}?api-version={_options.ApiVersion}";
        return await GetAsync<AdoWorkItem>(url, cancellationToken);
    }

    public async Task<AdoWorkItem?> CreateWorkItemAsync(string project, string type, Dictionary<string, object> fields, CancellationToken cancellationToken = default)
    {
        var patchDocument = fields.Select(field => new JsonPatchOperation
        {
            Op = "add",
            Path = $"/fields/{field.Key}",
            Value = field.Value
        }).ToList();

        var url = $"{Uri.EscapeDataString(project)}/_apis/wit/workitems/${Uri.EscapeDataString(type)}?api-version={_options.ApiVersion}";

        try
        {
            Logger.LogDebug("Sending Create Work Item request to {RequestUri}", url);

            var content = JsonContent.Create(patchDocument, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json-patch+json"), JsonSerializerOptions);
            var response = await HttpClient.PostAsync(url, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AdoWorkItem>(JsonSerializerOptions, cancellationToken);
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Logger.LogError("Failed to create work item. Status: {Status}, Content: {Content}", response.StatusCode, errorContent);
            response.EnsureSuccessStatusCode();
            return null; // Unreachable, EnsureSuccessStatusCode throws
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
        const int batchSize = 20;

        foreach (var batch in workItems.Chunk(batchSize))
        {
            var tasks = batch.Select(workItem => FetchWorkLogEntriesAsync(workItem, from, to, cancellationToken));
            var batchResults = await Task.WhenAll(tasks);
            result.AddRange(batchResults.SelectMany(x => x));
        }

        return result;
    }

    private async Task<List<AdoWorkLogEntry>> FetchWorkLogEntriesAsync(AdoWorkItem workItem, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var updatesUrl = $"_apis/wit/workitems/{workItem.Id}/updates?api-version={_options.ApiVersion}";
        var updatesResponse = await GetAsync<AdoWorkItemUpdatesResponse>(updatesUrl, cancellationToken);

        if (updatesResponse == null || updatesResponse.Value.Count == 0)
        {
            return [];
        }

        var entries = new List<AdoWorkLogEntry>();

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

            entries.Add(new AdoWorkLogEntry
            {
                WorkItemId = workItem.Id,
                Project = project?.ToString(),
                UserUniqueName = update.RevisedBy?.UniqueName,
                UserDisplayName = update.RevisedBy?.DisplayName,
                Hours = delta,
                LoggedAt = update.RevisedDate
            });
        }

        return entries;
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
