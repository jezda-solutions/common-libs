using System.Net.Http.Json;
using Jezda.Common.Integrations.AzureDevOps.Configuration;
using Jezda.Common.Integrations.AzureDevOps.Models;
using Jezda.Common.Integrations.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jezda.Common.Integrations.AzureDevOps;

public sealed class AzureDevOpsClient(
    HttpClient httpClient,
    IOptions<AzureDevOpsOptions> options,
    ILogger<AzureDevOpsClient> logger) : BaseIntegrationClient(httpClient, logger), IAzureDevOpsClient
{
    private readonly AzureDevOpsOptions _options = options.Value;

    /// <inheritdoc />
    public async Task<List<AdoWorkItem>> GetWorkItemsByQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        var wiqlRequest = new AdoWiqlRequest { Query = query };
        var wiqlUrl = $"_apis/wit/wiql?api-version={_options.ApiVersion}";
        var wiqlResponse = await PostAsync<AdoWiqlRequest, AdoWiqlResponse>(wiqlUrl, wiqlRequest, cancellationToken);

        // A link query answers in `workItemRelations` and leaves `workItems` empty, which would
        // read as "nothing matched". A list of work items also has no place for the links.
        if (wiqlResponse?.QueryType is { } queryType && !queryType.Equals("flat", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                $"GetWorkItemsByQueryAsync supports only flat queries (FROM WorkItems), but Azure DevOps ran this one as '{queryType}'.");
        }

        if (wiqlResponse == null || !wiqlResponse.WorkItems.Any())
        {
            return [];
        }

        return await AzureDevOpsWorkItemBatch.FetchAsync(
            wiqlResponse.WorkItems.Select(wi => wi.Id),
            _options.ApiVersion,
            (url, ct) => GetAsync<AdoWorkItemListResponse>(url, ct),
            Logger,
            cancellationToken);
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
            EnsureApiResponse(response);

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

    public async Task<List<AdoProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var url = $"_apis/projects?api-version={_options.ApiVersion}";
        var response = await GetAsync<AdoProjectListResponse>(url, cancellationToken);
        return response?.Value ?? new List<AdoProject>();
    }

    /// <summary>
    /// A sign-in page or a redirect to one throws as 401, any other redirect or a success without JSON
    /// with its own status; see <see cref="AzureDevOpsHttp.ThrowIfNotApiResponse"/>.
    /// </summary>
    protected override void EnsureApiResponse(HttpResponseMessage response) =>
        AzureDevOpsHttp.ThrowIfNotApiResponse(response);
}
