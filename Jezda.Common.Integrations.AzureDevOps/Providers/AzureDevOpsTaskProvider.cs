using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Jezda.Common.Integrations.AzureDevOps.Configuration;
using Jezda.Common.Integrations.AzureDevOps.Models;
using Jezda.Common.Integrations.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
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
    private const int MaxConcurrentLookups = 5;
    private const string ContinuationTokenHeader = "x-ms-continuationtoken";
    private readonly string _apiVersion = options.Value.ApiVersion;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExternalProvider Provider => ExternalProvider.AzureDevOps;

    /// <summary>
    /// Checks whether Azure DevOps accepts the token for the organisation, for both projects and work items.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the token is accepted; <see langword="false"/> when Azure DevOps
    /// rejects it on either request: 401, 403, or a sign-in page in place of JSON.
    /// </returns>
    /// <remarks>
    /// Listing projects needs "Project and Team (Read)", and listing work item fields needs
    /// "Work Items (Read)". Checking only the first would confirm a PAT that fails on the first sync.
    /// <para />
    /// Only a rejected token answers <see langword="false"/>. A failure that says nothing about the
    /// token propagates instead, so an outage is not reported to the user as a bad PAT. That includes
    /// the resilience pipeline's own exceptions, all deriving from Polly's
    /// <c>ExecutionRejectedException</c> and none from <see cref="HttpRequestException"/>:
    /// <c>TimeoutRejectedException</c> for a timeout, <c>BrokenCircuitException</c> for an open circuit
    /// and <c>RateLimiterRejectedException</c> for too many concurrent requests.
    /// <para />
    /// A 403 can also come from Conditional Access, an IP allow-list or a WAF, for a valid PAT. It still
    /// answers <see langword="false"/>, with the response body in the log to tell these apart.
    /// </remarks>
    /// <exception cref="HttpRequestException">
    /// Any other failed status, such as 5xx, or a redirect that is not to a sign-in page. Also a success
    /// that is not JSON, 204 included, since neither checked the token: the base URL answers somewhere
    /// else, or something other than Azure DevOps answered. The status code is on the exception.
    /// </exception>
    /// <exception cref="OperationCanceledException">The call was cancelled.</exception>
    public async Task<bool> ValidateConnectionAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));

        using var client = CreateClient(accessToken, baseUrl);

        var accepted = await IsTokenAcceptedAsync(client, $"_apis/projects?$top=1&api-version={_apiVersion}", cancellationToken)
            && await IsTokenAcceptedAsync(client, $"_apis/wit/fields?api-version={_apiVersion}", cancellationToken);

        if (accepted)
        {
            logger.LogInformation(
                "Azure DevOps accepted the token for projects and work items on {Organisation}",
                AzureDevOpsHttp.OrganisationForLog(client));
        }

        return accepted;
    }

    private async Task<bool> IsTokenAcceptedAsync(HttpClient client, string url, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken);

        var isSignInPage = AzureDevOpsHttp.IsSignInPage(response);

        if (isSignInPage || response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            logger.LogWarning(
                "Azure DevOps rejected the token with {StatusCode} on {Path} for {Organisation}: {Answer}",
                (int)response.StatusCode,
                url.Split('?')[0],
                AzureDevOpsHttp.OrganisationForLog(client),
                isSignInPage ? "a sign-in page" : await ReadErrorBodyAsync(response, cancellationToken));
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

            var page = await httpResponse.Content.ReadFromUtf8JsonAsync<AdoProjectListResponse>(JsonOptions, cancellationToken);
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
    /// project in the result costs one more request, at most five at once, and an item whose project
    /// answers 404 cannot be bound to one and is dropped. A 403 stays an error, because it can mean a
    /// PAT without the scope, which should not look like results that silently went missing.
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
    /// Looks up each named project once, a few at a time because a person is waiting on the search. A
    /// project that answers 404 is left out; any other failure propagates and cancels the lookups
    /// still running, so an expired token still surfaces as one.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, AdoProject>> FindProjectsAsync(
        HttpClient client,
        IEnumerable<string> names,
        CancellationToken cancellationToken)
    {
        var found = new ConcurrentDictionary<string, AdoProject>(StringComparer.Ordinal);
        var distinct = names.Where(name => name.Length > 0).Distinct(StringComparer.Ordinal);
        var options = new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentLookups, CancellationToken = cancellationToken };

        await Parallel.ForEachAsync(distinct, options, async (name, token) =>
        {
            try
            {
                found[name] = await GetProjectAsync(client, name, token);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogWarning("Azure DevOps search returned a work item in {Project}, which answers 404", name);
            }
        });

        return found;
    }

    /// <param name="client">The client created for the organisation.</param>
    /// <param name="idOrName">The project's GUID or its name; Azure DevOps accepts both here.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    private async Task<AdoProject> GetProjectAsync(HttpClient client, string idOrName, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"_apis/projects/{Uri.EscapeDataString(idOrName)}?api-version={_apiVersion}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromUtf8JsonAsync<AdoProject>(JsonOptions, cancellationToken)
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

        var result = await response.Content.ReadFromUtf8JsonAsync<AdoWiqlResponse>(JsonOptions, cancellationToken);

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
    /// A sign-in page or a redirect to one throws as 401, and any other redirect or a success without
    /// JSON with its own status; see <see cref="AzureDevOpsHttp.ThrowIfNotApiResponse"/>.
    /// </para>
    /// </remarks>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        AzureDevOpsHttp.ThrowIfNotApiResponse(response);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await ReadErrorBodyAsync(response, cancellationToken);

        throw new HttpRequestException(
            $"Azure DevOps returned {(int)response.StatusCode} ({response.ReasonPhrase}): {body}",
            inner: null,
            statusCode: response.StatusCode);
    }

    // Best effort: a body that cannot be read must not replace the status code, which is what callers
    // branch on, so the filter catches a connection that drops mid-body and content already disposed
    // (ObjectDisposedException is an InvalidOperationException), and content that cannot be
    // buffered (NotSupportedException). The declared charset cannot fail it:
    // the body is read as UTF-8 regardless; see HttpContentUtf8Extensions.
    // The base URL comes from the user, so the body can be anything: control characters go, because a
    // line break in it would forge an entry in a plain-text log.
    private static async Task<string> ReadErrorBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string body;
        try
        {
            body = await response.Content.ReadAsUtf8StringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException or NotSupportedException)
        {
            return string.Empty;
        }

        return string.Concat((body.Length > 500 ? body[..500] : body).Select(c => char.IsControl(c) ? ' ' : c));
    }

    /// <summary>
    /// Hydrates work item ids into full work items. The result can hold fewer items than ids were
    /// asked for; see <see cref="AzureDevOpsWorkItemBatch.FetchAsync"/>.
    /// </summary>
    private Task<List<AdoWorkItem>> FetchWorkItemDetailsAsync(
        HttpClient client,
        IEnumerable<int> ids,
        CancellationToken cancellationToken) =>
        AzureDevOpsWorkItemBatch.FetchAsync(
            ids,
            _apiVersion,
            AzureDevOpsHttp.OrganisationForLog(client),
            async (url, ct) =>
            {
                using var httpResponse = await client.GetAsync(url, ct);
                await EnsureSuccessAsync(httpResponse, ct);

                return await httpResponse.Content.ReadFromUtf8JsonAsync<AdoWorkItemListResponse>(JsonOptions, ct);
            },
            logger,
            cancellationToken);

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
        // Trailing slash added; query, fragment and any username/password typed into the URL by
        // mistake dropped (the token goes only in the header below); see IntegrationBaseAddress.
        client.BaseAddress = IntegrationBaseAddress.Create(baseUrl);

        var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($":{accessToken}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

        return client;
    }
}
