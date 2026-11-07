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
    /// Lists a project's issues.
    /// </summary>
    /// <remarks>
    /// Each issue is asked to carry its project, which supplies the durable id whichever of id or key
    /// <paramref name="projectId"/> was. A bare number is the project id in JQL; a key is quoted, and
    /// cannot be all digits.
    /// </remarks>
    public async Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(string accessToken, string projectId, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));
        // Blank would otherwise pass the digit check and build a JQL Jira rejects.
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        using var client = CreateClient(accessToken, baseUrl);

        var projectClause = projectId.All(char.IsAsciiDigit)
            ? projectId
            : $"\"{projectId.Replace("\"", "\\\"")}\"";

        var searchRequest = new JiraSearchRequest
        {
            Jql = $"project = {projectClause}",
            StartAt = 0,
            MaxResults = 50,
            Fields = ["summary", "status", "project"]
        };

        using var response = await client.PostAsJsonAsync("rest/api/3/search", searchRequest, JsonOptions, cancellationToken);
        JiraHttp.ThrowIfNotApiResponse(response);
        response.EnsureSuccessStatusCode();

        var searchResponse = await response.Content.ReadFromUtf8JsonAsync<JiraSearchResponse>(JsonOptions, cancellationToken);
        var site = client.BaseAddress!;

        return [.. (searchResponse?.Issues ?? []).Select(i => new ExternalTaskDto
        {
            Id = i.Key,
            Title = i.Fields?.Summary ?? string.Empty,
            Status = i.Fields?.Status?.Name,
            Url = new Uri(site, $"browse/{i.Key}").AbsoluteUri,
            ProjectId = i.Fields?.Project?.Id ?? projectId,
            Provider = ExternalProvider.Jira
        })];
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
