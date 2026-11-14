using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Jezda.Common.Integrations.Http;
using Jezda.Common.Integrations.Jira.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Jezda.Common.Integrations.Jira.Providers;

public sealed class JiraTaskProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<JiraTaskProvider> logger) : IExternalTaskProvider
{
    public const string HttpClientName = "ExternalTaskProvider.Jira";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExternalProvider Provider => ExternalProvider.Jira;

    /// <summary>
    /// Formats the access token for Jira Basic auth from separate email and API token.
    /// The returned string should be passed as the <c>accessToken</c> parameter.
    /// </summary>
    public static string FormatAccessToken(string email, string apiToken) => $"{email}:{apiToken}";

    public async Task<ConnectionValidationResult> ValidateConnectionAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));

        using var client = CreateClient(accessToken, baseUrl);
        using var response = await client.GetAsync("rest/api/3/myself", cancellationToken);

        // Jira's CAPTCHA refuses the login without checking the credentials, so they may be valid.
        if (response.Headers.TryGetValues("X-Seraph-LoginReason", out var reasons)
            && reasons.Any(reason => reason.Contains("AUTHENTICATION_DENIED", StringComparison.OrdinalIgnoreCase)))
        {
            throw new HttpRequestException(
                "Jira refused the login because its CAPTCHA was triggered. Sign in through the Jira website, then retry.",
                null,
                response.StatusCode);
        }

        // A 403 can come from an IP allow list or a gateway in front of Jira, with a valid token.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogWarning("Jira rejected the token with {StatusCode}", (int)response.StatusCode);
            return ConnectionValidationResult.Rejected(TokenRejectionReason.InvalidToken, "401 on rest/api/3/myself");
        }

        JiraHttp.ThrowIfNotApiResponse(response);
        response.EnsureSuccessStatusCode();

        return ConnectionValidationResult.Accepted;
    }

    public async Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));

        using var client = CreateClient(accessToken, baseUrl);

        using var response = await client.GetAsync("rest/api/3/project", cancellationToken);
        JiraHttp.ThrowIfNotApiResponse(response);
        response.EnsureSuccessStatusCode();

        var projects = await response.Content.ReadFromUtf8JsonAsync<List<JiraProject>>(JsonOptions, cancellationToken)
                       ?? [];
        var site = client.BaseAddress!;

        return [.. projects.Select(p => new ExternalProjectDto
        {
            // An admin can change a project's key, so only the numeric id is durable.
            Id = p.Id,
            Path = p.Key,
            Name = p.Name,
            Description = null,
            Url = new Uri(site, $"browse/{p.Key}").AbsoluteUri,
            Provider = ExternalProvider.Jira,
            IsArchived = p.Archived
        })];
    }

    /// <summary>
    /// Lists all of a project's issues, following Jira's page tokens to the last page.
    /// </summary>
    /// <remarks>
    /// Each issue is asked to carry its project, which supplies the durable id whichever of id or key
    /// <paramref name="projectId"/> was. A bare number is the project id in JQL; a key is quoted, and
    /// cannot be all digits. If no issue carries the project, a key is resolved to the id with one
    /// more request, so <see cref="ExternalTaskDto.ProjectId"/> is never a key an admin can change.
    /// </remarks>
    public async Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(string accessToken, string projectId, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));
        // Blank would otherwise pass the digit check and build a JQL Jira rejects.
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        using var client = CreateClient(accessToken, baseUrl);

        var isNumericId = projectId.All(char.IsAsciiDigit);
        var projectClause = isNumericId
            ? projectId
            : $"\"{projectId.Replace("\"", "\\\"")}\"";

        var issues = await SearchAllAsync(client, $"project = {projectClause}", cancellationToken);
        if (issues.Count == 0)
        {
            return [];
        }

        // The query names one project, so every issue belongs to it.
        var durableProjectId = issues.Select(i => i.Fields?.Project?.Id).FirstOrDefault(id => !string.IsNullOrEmpty(id))
                               ?? (isNumericId ? projectId : await GetProjectIdAsync(client, projectId, cancellationToken));
        var site = client.BaseAddress!;

        return [.. issues.Select(i => new ExternalTaskDto
        {
            Id = i.Key,
            Title = i.Fields?.Summary ?? string.Empty,
            Status = i.Fields?.Status?.Name,
            Url = new Uri(site, $"browse/{i.Key}").AbsoluteUri,
            ProjectId = durableProjectId,
            Provider = ExternalProvider.Jira
        })];
    }

    /// <summary>
    /// Pages through <c>POST rest/api/3/search/jql</c>. The offset-based <c>rest/api/3/search</c> is
    /// gone (410); this one has no offset and no total, only a token for the next page.
    /// </summary>
    private static async Task<List<JiraIssue>> SearchAllAsync(HttpClient client, string jql, CancellationToken cancellationToken)
    {
        var all = new List<JiraIssue>();
        var seenTokens = new HashSet<string>(StringComparer.Ordinal);
        string? nextPageToken = null;

        do
        {
            var searchRequest = new JiraSearchRequest
            {
                Jql = jql,
                NextPageToken = nextPageToken,
                // Jira's cap when issue fields are requested.
                MaxResults = 100,
                Fields = ["summary", "status", "project"]
            };

            using var response = await client.PostAsJsonAsync("rest/api/3/search/jql", searchRequest, JsonOptions, cancellationToken);
            JiraHttp.ThrowIfNotApiResponse(response);
            response.EnsureSuccessStatusCode();

            var page = await response.Content.ReadFromUtf8JsonAsync<JiraSearchResponse>(JsonOptions, cancellationToken);
            all.AddRange(page?.Issues ?? []);

            nextPageToken = page is { IsLast: not true } ? page.NextPageToken : null;

            // A token Jira already gave would page forever.
            if (nextPageToken is not null && !seenTokens.Add(nextPageToken))
            {
                throw new HttpRequestException(
                    "Jira returned the same page token twice; the issue list would never end.",
                    null,
                    response.StatusCode);
            }
        }
        while (!string.IsNullOrEmpty(nextPageToken));

        return all;
    }

    private static async Task<string> GetProjectIdAsync(HttpClient client, string projectKey, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"rest/api/3/project/{Uri.EscapeDataString(projectKey)}", cancellationToken);
        JiraHttp.ThrowIfNotApiResponse(response);
        response.EnsureSuccessStatusCode();

        var project = await response.Content.ReadFromUtf8JsonAsync<JiraProject>(JsonOptions, cancellationToken);
        if (string.IsNullOrEmpty(project?.Id))
        {
            throw new HttpRequestException(
                $"Jira returned project '{projectKey}' without an id.",
                null,
                response.StatusCode);
        }

        return project.Id;
    }

    private HttpClient CreateClient(string accessToken, string baseUrl)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        // Trailing slash added; query, fragment and any username/password typed into the URL by
        // mistake dropped (the token goes only in the header below); see IntegrationBaseAddress.
        client.BaseAddress = IntegrationBaseAddress.Create(baseUrl);

        // ASCII would turn a character outside its range into '?', and a valid email would read as rejected.
        var base64Auth = Convert.ToBase64String(Encoding.UTF8.GetBytes(accessToken));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Auth);

        return client;
    }
}
