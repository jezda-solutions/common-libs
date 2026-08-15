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
        var wiqlRequest = new AdoWiqlRequest
        {
            Query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{EscapeWiql(projectId)}' AND [System.State] <> 'Removed' ORDER BY [System.Id] DESC"
        };

        var references = await RunWiqlAsync(
            client, wiqlRequest, $"{Uri.EscapeDataString(projectId)}/_apis/wit/wiql", cancellationToken);

        if (references.Count == 0)
        {
            return [];
        }

        var workItems = await FetchWorkItemDetailsAsync(
            client, references.Select(wi => wi.Id), cancellationToken);

        return [.. workItems.Select(wi => ToExternalTask(wi, projectId))];
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
    /// what they were working on. <c>TOP</c> applies that ordering server-side, so the rows the
    /// caller keeps are chosen by Azure DevOps rather than by trimming an unbounded id list here —
    /// a common term against a large organisation otherwise returns thousands of refs to discard.
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

        var wiqlRequest = new AdoWiqlRequest
        {
            Query = $"SELECT TOP {limit} [System.Id] FROM WorkItems WHERE [System.Title] CONTAINS '{EscapeWiql(searchTerm.Trim())}' AND [System.State] <> 'Removed' ORDER BY [System.ChangedDate] DESC"
        };

        // No project segment in the path: that is what makes this organisation-wide.
        var references = await RunWiqlAsync(client, wiqlRequest, "_apis/wit/wiql", cancellationToken);

        if (references.Count == 0)
        {
            return [];
        }

        // TOP already bounded the query, but a server that ignores it must not turn into an
        // unbounded details batch.
        var ids = references.Take(limit).Select(wi => wi.Id).ToList();
        var workItems = await FetchWorkItemDetailsAsync(client, ids, cancellationToken);

        // The details batch does not contract to return items in the order the ids were supplied,
        // and the WIQL's ORDER BY is the whole reason the newest-changed item should be first.
        // Re-key on the requested order; the lookup also drops anything deleted between the calls.
        var byId = workItems.ToDictionary(wi => wi.Id);

        return
        [
            .. ids.Where(byId.ContainsKey)
                  .Select(id => ToExternalTask(byId[id], byId[id].TeamProject))
        ];
    }

    /// <summary>
    /// Escapes a value for interpolation into a WIQL string literal.
    /// </summary>
    /// <remarks>
    /// WIQL string literals are single-quoted, so an apostrophe in user text — a project named
    /// "Bob's team", a search for "it's broken" — would otherwise close the literal early and
    /// change the query's meaning.
    /// </remarks>
    private static string EscapeWiql(string value) => value.Replace("'", "''");

    /// <summary>
    /// Posts a WIQL query and returns the work item references it matched.
    /// </summary>
    /// <param name="path">
    /// The WIQL endpoint without its query string. Project-scoped for <see cref="GetTasksAsync"/>,
    /// organisation-wide for <see cref="SearchTasksAsync"/> — the only difference between them.
    /// </param>
    private async Task<IReadOnlyList<AdoWorkItemReference>> RunWiqlAsync(
        HttpClient client,
        AdoWiqlRequest wiqlRequest,
        string path,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync(
            $"{path}?api-version={_apiVersion}", wiqlRequest, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<AdoWiqlResponse>(JsonOptions, cancellationToken);

        return result?.WorkItems ?? [];
    }

    /// <summary>
    /// Hydrates work item ids into full work items.
    /// </summary>
    /// <remarks>
    /// Chunked at 200 because that is the maximum number of ids Azure DevOps accepts on a single
    /// <c>workitems?ids=</c> request; beyond it the call fails outright rather than truncating.
    /// </remarks>
    private async Task<List<AdoWorkItem>> FetchWorkItemDetailsAsync(
        HttpClient client,
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
    {
        var workItems = new List<AdoWorkItem>();

        foreach (var batch in ids.Chunk(200))
        {
            var idsString = string.Join(",", batch);
            var response = await client.GetFromJsonAsync<AdoWorkItemListResponse>(
                $"_apis/wit/workitems?ids={idsString}&api-version={_apiVersion}", JsonOptions, cancellationToken);

            if (response?.Value is not null)
            {
                workItems.AddRange(response.Value);
            }
        }

        return workItems;
    }

    /// <summary>
    /// Projects a work item onto the shared DTO.
    /// </summary>
    /// <param name="projectId">
    /// Must equal what <see cref="GetProjectsAsync"/> reports as <c>ExternalProjectDto.Id</c> — the
    /// project <i>name</i>, not its GUID. Consumers key stored rows on it. A project-scoped read
    /// passes the project it asked for; the organisation-wide search reads it back off the work
    /// item, because it did not name one.
    /// </param>
    private static ExternalTaskDto ToExternalTask(AdoWorkItem workItem, string projectId) => new()
    {
        Id = workItem.Id.ToString(),
        Title = workItem.Title,
        Status = workItem.State,
        Url = workItem.Url,
        ProjectId = projectId,
        Provider = ExternalProvider.AzureDevOps
    };

    private HttpClient CreateClient(string accessToken, string baseUrl)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");

        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{accessToken}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

        return client;
    }
}
