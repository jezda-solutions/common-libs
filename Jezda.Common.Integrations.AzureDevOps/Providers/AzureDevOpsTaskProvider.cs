using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Jezda.Common.Integrations.AzureDevOps.Configuration;
using Jezda.Common.Integrations.AzureDevOps.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
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
    private const int ProjectPageSize = 100;
    private const string ContinuationTokenHeader = "x-ms-continuationtoken";
    private readonly string _apiVersion = options.Value.ApiVersion;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExternalProvider Provider => ExternalProvider.AzureDevOps;

    /// <summary>
    /// Checks whether Azure DevOps accepts the token for the organisation, for both projects and
    /// work items.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the token is accepted; <see langword="false"/> when Azure DevOps
    /// rejects it: 401, 403, or a sign-in page in place of JSON.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Two requests, both organisation-level and small. <c>_apis/projects</c> needs the
    /// "Project and Team (Read)" scope (<c>vso.project</c>), which <see cref="GetProjectsAsync"/> and
    /// every work item call need too. <c>_apis/wit/fields/System.Id</c> needs "Work Items (Read)"
    /// (<c>vso.work</c>), which <see cref="GetTasksAsync"/> and <see cref="SearchTasksAsync"/> need:
    /// without it a PAT with only the project scope would validate and then fail on the first sync.
    /// The second request is not sent when the first already rejects the token.
    /// </para>
    /// <para>
    /// Only a rejected token answers <see langword="false"/>. A failure that says nothing about the
    /// token propagates instead, so an outage is not reported to the user as a bad PAT. That includes
    /// the resilience pipeline's own exceptions: Polly's <c>TimeoutRejectedException</c> for a timeout
    /// and <c>BrokenCircuitException</c> for an open circuit, neither of which derives from
    /// <see cref="HttpRequestException"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="HttpRequestException">
    /// Any other failed status, such as 5xx, or a redirect that is not to a sign-in page: the base URL
    /// answers somewhere else. The status code is on the exception.
    /// </exception>
    /// <exception cref="OperationCanceledException">The call was cancelled.</exception>
    public async Task<bool> ValidateConnectionAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));

        using var client = CreateClient(accessToken, baseUrl);

        return await IsAcceptedAsync(client, $"_apis/projects?$top=1&api-version={_apiVersion}", "Project and Team (Read)", cancellationToken)
            && await IsAcceptedAsync(client, $"_apis/wit/fields/System.Id?api-version={_apiVersion}", "Work Items (Read)", cancellationToken);
    }

    /// <summary>
    /// Sends one validation request: <see langword="false"/> for a rejected token, an exception for
    /// any other failure.
    /// </summary>
    private async Task<bool> IsAcceptedAsync(HttpClient client, string url, string scope, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            || AzureDevOpsHttp.IsSignInPage(response))
        {
            logger.LogWarning(
                "Azure DevOps rejected the token with {StatusCode} on the {Scope} check",
                (int)response.StatusCode,
                scope);
            return false;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));

        using var client = CreateClient(accessToken, baseUrl);

        var projects = new List<AdoProject>();
        string? continuationToken = null;

        do
        {
            var url = $"_apis/projects?$top={ProjectPageSize}&api-version={_apiVersion}";
            if (continuationToken is not null)
            {
                url += $"&continuationToken={Uri.EscapeDataString(continuationToken)}";
            }

            using var httpResponse = await client.GetAsync(url, cancellationToken);
            await EnsureSuccessAsync(httpResponse, cancellationToken);

            var page = await httpResponse.Content.ReadFromJsonAsync<AdoProjectListResponse>(JsonOptions, cancellationToken);
            projects.AddRange(page?.Value ?? []);

            continuationToken = httpResponse.Headers.TryGetValues(ContinuationTokenHeader, out var values)
                ? values.FirstOrDefault()
                : null;
        }
        while (!string.IsNullOrEmpty(continuationToken));

        return [.. projects.Select(p => ToExternalProject(p, client.BaseAddress!))];
    }

    /// <summary>
    /// Lists a project's work items.
    /// </summary>
    /// <remarks>
    /// The project is looked up first because <c>[System.TeamProject]</c> in WIQL takes the name, and
    /// a work item carries only the name while <c>ProjectId</c> must be the GUID.
    /// <c>_apis/projects/{id}</c> accepts both the GUID and the name, and the lookup by GUID keeps
    /// working after a rename.
    /// </remarks>
    public async Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(
        string accessToken,
        string projectId,
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));
        // Blank would otherwise address the project listing and parse as a nameless project.
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        using var client = CreateClient(accessToken, baseUrl);

        var project = await GetProjectAsync(client, projectId, cancellationToken);

        // Execute WIQL query to get work item IDs
        var wiqlRequest = new AdoWiqlRequest
        {
            Query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{EscapeWiql(project.Name)}' AND [System.State] <> 'Removed' ORDER BY [System.Id] DESC"
        };

        var references = await RunWiqlAsync(
            client, wiqlRequest, $"{Uri.EscapeDataString(project.Name)}/_apis/wit/wiql", cancellationToken);

        if (references.Count == 0)
        {
            return [];
        }

        var workItems = await FetchWorkItemDetailsAsync(
            client, references.Select(wi => wi.Id), cancellationToken);

        return [.. workItems.Select(wi => ToExternalTask(wi, project.Id, project.Name, client.BaseAddress!))];
    }

    /// <summary>
    /// Searches work items by title across the whole organisation — one WIQL query plus one details
    /// batch, instead of the base implementation's query-per-project fan-out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The WIQL omits <c>[System.TeamProject]</c> and the URL omits the project segment, which is
    /// what makes this organisation-wide. Consequently the project cannot come from a parameter —
    /// it is read back per work item from <c>System.TeamProject</c>. That field is the project's
    /// <i>name</i>, while <c>ProjectId</c> must equal the GUID <see cref="GetProjectsAsync"/> reports
    /// as <c>ExternalProjectDto.Id</c>, because consumers key stored rows on it. So each distinct
    /// project in the result costs one more request, and an item whose project answers 404 cannot be
    /// bound to one and is dropped.
    /// </para>
    /// <para>
    /// <c>CONTAINS</c> is a substring match on the title only, and is what Azure DevOps can index;
    /// <c>CONTAINS WORDS</c> would be full-text but requires the search extension to be installed.
    /// Results come back newest-changed first, which is the useful order for someone looking for
    /// what they were working on. The <c>$top</c> URL parameter applies that ordering server-side, so
    /// the rows the caller keeps are chosen by Azure DevOps rather than by trimming an unbounded id
    /// list here — a common term against a large organisation otherwise returns thousands of refs to
    /// discard. It has to be the URL parameter: WIQL has no <c>TOP</c> keyword, and a query that
    /// contains one is rejected with 400 Bad Request.
    /// </para>
    /// <para>
    /// <b>A number is also a work item id.</b> Developers usually know the id (<c>1234</c>, or
    /// <c>#1234</c> as Azure DevOps writes it) better than the title. Such a term runs a second, tiny
    /// WIQL on <c>[System.Id]</c> next to the title search, and its hit goes first. It is a separate
    /// query rather than an <c>OR</c> in the title one because that query is cut by <c>$top</c> after
    /// ordering by change date: an older item asked for by its number would be cut off by newer titles
    /// that merely contain the same digits.
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

        var term = searchTerm.Trim();
        var exactId = TryParseWorkItemId(term);
        var titleTerm = exactId is not null && term.StartsWith('#') ? term[1..] : term;

        // The id lookup first, unbounded by the title search's $top. It matches at most one row:
        // ids are unique across the organisation.
        var exactIds = new List<int>();
        if (exactId is { } wanted)
        {
            var idQuery = new AdoWiqlRequest
            {
                Query = $"SELECT [System.Id] FROM WorkItems WHERE [System.Id] = {wanted} AND [System.State] <> 'Removed'"
            };
            exactIds.AddRange((await RunWiqlAsync(client, idQuery, "_apis/wit/wiql", cancellationToken)).Select(wi => wi.Id));
        }

        var wiqlRequest = new AdoWiqlRequest
        {
            Query = $"SELECT [System.Id] FROM WorkItems WHERE [System.Title] CONTAINS '{EscapeWiql(titleTerm)}' AND [System.State] <> 'Removed' ORDER BY [System.ChangedDate] DESC"
        };

        // No project segment in the path: that is what makes this organisation-wide.
        var references = await RunWiqlAsync(client, wiqlRequest, "_apis/wit/wiql", cancellationToken, top: limit);

        // $top already bounded the title query, but a server that ignores it must not turn into an
        // unbounded details batch. The exact id leads; a title hit on the same item is not repeated.
        var ids = exactIds
            .Concat(references.Select(wi => wi.Id))
            .Distinct()
            .Take(limit)
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        var workItems = await FetchWorkItemDetailsAsync(client, ids, cancellationToken);

        // The details batch does not contract to return items in the order the ids were supplied,
        // and the WIQL's ORDER BY is the whole reason the newest-changed item should be first.
        // Re-key on the requested order; the lookup also drops anything deleted between the calls.
        var byId = workItems.ToDictionary(wi => wi.Id);
        var ordered = ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();

        var projects = await FindProjectsAsync(client, ordered.Select(wi => wi.TeamProject), cancellationToken);

        return
        [
            .. ordered
                .Where(wi => wi.TeamProject.Length == 0 || projects.ContainsKey(wi.TeamProject))
                .Select(wi => projects.TryGetValue(wi.TeamProject, out var project)
                    ? ToExternalTask(wi, project.Id, project.Name, client.BaseAddress!)
                    : ToExternalTask(wi, string.Empty, string.Empty, client.BaseAddress!))
        ];
    }

    /// <summary>
    /// Looks up each named project once, concurrently because a person is waiting on the search. A
    /// project that answers 404 is left out; any other failure propagates, so an expired token still
    /// surfaces as one.
    /// </summary>
    private async Task<Dictionary<string, AdoProject>> FindProjectsAsync(
        HttpClient client,
        IEnumerable<string> names,
        CancellationToken cancellationToken)
    {
        var lookups = names
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Select(async name =>
            {
                try
                {
                    return (Name: name, Project: (AdoProject?)await GetProjectAsync(client, name, cancellationToken));
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    logger.LogWarning("Azure DevOps search returned a work item in {Project}, which answers 404", name);
                    return (Name: name, Project: null);
                }
            });

        var found = await Task.WhenAll(lookups);

        return found
            .Where(l => l.Project is not null)
            .ToDictionary(l => l.Name, l => l.Project!, StringComparer.Ordinal);
    }

    /// <param name="client">The client created for the organisation.</param>
    /// <param name="idOrName">The project's GUID or its name; Azure DevOps accepts both here.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    private async Task<AdoProject> GetProjectAsync(HttpClient client, string idOrName, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"_apis/projects/{Uri.EscapeDataString(idOrName)}?api-version={_apiVersion}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<AdoProject>(JsonOptions, cancellationToken)
               ?? throw new HttpRequestException($"Azure DevOps returned an empty project for {idOrName}");
    }

    private static ExternalProjectDto ToExternalProject(AdoProject project, Uri organisationUrl) => new()
    {
        Id = project.Id,
        Path = project.Name,
        Name = project.Name,
        Description = project.Description,
        // The project's own `url` is the REST resource (`_apis/projects/{id}`), which answers with
        // JSON, not the project page.
        Url = new Uri(organisationUrl, Uri.EscapeDataString(project.Name)).AbsoluteUri,
        Provider = ExternalProvider.AzureDevOps,
        // The list does not report a creation date. `lastUpdateTime` absent parses to the default.
        UpdatedAt = project.LastUpdateTime == default ? null : project.LastUpdateTime,
        IsArchived = project.State is null
            ? null
            : project.State.Equals("deleting", StringComparison.OrdinalIgnoreCase)
              || project.State.Equals("deleted", StringComparison.OrdinalIgnoreCase),
        // Anything short of public is hidden from the public, the way GitHub reports `internal`.
        IsPrivate = project.Visibility is null
            ? null
            : !project.Visibility.Equals("public", StringComparison.OrdinalIgnoreCase)
    };

    /// <summary>
    /// Reads a search term as a work item id: digits only, optionally prefixed with <c>#</c>.
    /// </summary>
    private static int? TryParseWorkItemId(string term)
    {
        var digits = term.StartsWith('#') ? term[1..] : term;

        return digits.Length > 0
            && digits.All(char.IsAsciiDigit)
            && int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id)
            && id > 0
                ? id
                : null;
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
        CancellationToken cancellationToken,
        int? top = null)
    {
        var url = top is null
            ? $"{path}?api-version={_apiVersion}"
            : $"{path}?api-version={_apiVersion}&$top={top.Value}";

        using var response = await client.PostAsJsonAsync(url, wiqlRequest, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<AdoWiqlResponse>(JsonOptions, cancellationToken);

        return result?.WorkItems ?? [];
    }

    /// <summary>
    /// <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/>, but carrying the error Azure DevOps
    /// returned in the exception message.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A WIQL that Azure DevOps rejects comes back as 400 with the reason in the body
    /// (<c>TF51006: …</c>). The bare status says only "Bad Request", which hid a query bug in search
    /// until it was read off production. The status code stays on the exception, so callers that
    /// treat 401/403 as an expired token keep working.
    /// </para>
    /// <para>
    /// A sign-in page or a redirect to one throws as 401, and any other redirect with its own status;
    /// see <see cref="AzureDevOpsHttp.ThrowIfNotApiResponse"/>.
    /// </para>
    /// </remarks>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        AzureDevOpsHttp.ThrowIfNotApiResponse(response);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // Best effort: a body that cannot be read must not replace the status code, which is what
        // callers branch on.
        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            body = string.Empty;
        }

        if (body.Length > 500)
        {
            body = body[..500];
        }

        throw new HttpRequestException(
            $"Azure DevOps returned {(int)response.StatusCode} ({response.ReasonPhrase}): {body}",
            inner: null,
            statusCode: response.StatusCode);
    }

    /// <summary>
    /// Hydrates work item ids into full work items.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Chunked at 200 because that is the maximum number of ids Azure DevOps accepts on a single
    /// <c>workitems?ids=</c> request; beyond it the call fails outright rather than truncating.
    /// </para>
    /// <para>
    /// <c>errorPolicy=omit</c> because an id deleted or made unreadable after the WIQL ran would
    /// otherwise fail the whole batch with 404. Azure DevOps answers such an id with a null slot,
    /// which is dropped here, so the result can hold fewer items than ids were asked for.
    /// </para>
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
            using var httpResponse = await client.GetAsync(
                $"_apis/wit/workitems?ids={idsString}&errorPolicy=omit&api-version={_apiVersion}", cancellationToken);
            await EnsureSuccessAsync(httpResponse, cancellationToken);

            var response = await httpResponse.Content.ReadFromJsonAsync<AdoWorkItemListResponse>(JsonOptions, cancellationToken);

            if (response?.Value is not null)
            {
                workItems.AddRange(response.Value.OfType<AdoWorkItem>());
            }
        }

        return workItems;
    }

    /// <summary>
    /// Projects a work item onto the shared DTO.
    /// </summary>
    /// <param name="workItem">The work item to project.</param>
    /// <param name="projectId">
    /// The project's GUID, equal to what <see cref="GetProjectsAsync"/> reports as
    /// <c>ExternalProjectDto.Id</c>. Consumers key stored rows on it.
    /// </param>
    /// <param name="projectPath">The project's current name, which the work item's page link needs.</param>
    /// <param name="organisationUrl">The organisation base address the client was created with.</param>
    private static ExternalTaskDto ToExternalTask(AdoWorkItem workItem, string projectId, string projectPath, Uri organisationUrl) => new()
    {
        Id = workItem.Id.ToString(),
        Title = workItem.Title,
        Status = workItem.State,
        // Without a project there is no page to name; the REST link is still a real link to the item.
        Url = string.IsNullOrEmpty(projectPath) ? workItem.Url : WorkItemWebUrl(organisationUrl, projectPath, workItem.Id),
        ProjectId = projectId,
        Provider = ExternalProvider.AzureDevOps
    };

    /// <summary>
    /// The link a person opens in the browser: <c>{organisation}/{project}/_workitems/edit/{id}</c>.
    /// </summary>
    /// <remarks>
    /// The work item's own <c>url</c> is the REST resource (<c>_apis/wit/workItems/{id}</c>), which
    /// answers with JSON, not the work item page.
    /// </remarks>
    private static string WorkItemWebUrl(Uri organisationUrl, string projectName, int workItemId) =>
        new Uri(organisationUrl, $"{Uri.EscapeDataString(projectName)}/_workitems/edit/{workItemId}").AbsoluteUri;

    private HttpClient CreateClient(string accessToken, string baseUrl)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");

        var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{accessToken}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

        client.DefaultRequestHeaders.Add(AzureDevOpsHttp.FedAuthRedirectHeader, AzureDevOpsHttp.FedAuthRedirectSuppress);

        return client;
    }
}
