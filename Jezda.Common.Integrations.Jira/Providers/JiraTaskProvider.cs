using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Jezda.Common.Integrations.Jira.Models;
using Microsoft.Extensions.Logging;
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

    public async Task<bool> ValidateConnectionAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));

        using var client = CreateClient(accessToken, baseUrl);

        try
        {
            var response = await client.GetAsync("rest/api/3/myself", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Jira connection validation failed");
            return false;
        }
    }

    public async Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));

        using var client = CreateClient(accessToken, baseUrl);

        var projects = await client.GetFromJsonAsync<List<JiraProject>>("rest/api/3/project", JsonOptions, cancellationToken)
                       ?? [];

        return [.. projects.Select(p => new ExternalProjectDto
        {
            // An admin can change a project's key, so only the numeric id is durable.
            Id = p.Id,
            Path = p.Key,
            Name = p.Name,
            Description = null,
            Url = $"{baseUrl.TrimEnd('/')}/browse/{p.Key}",
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

        var response = await client.PostAsJsonAsync("rest/api/3/search", searchRequest, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var searchResponse = await response.Content.ReadFromJsonAsync<JiraSearchResponse>(JsonOptions, cancellationToken);

        return [.. (searchResponse?.Issues ?? []).Select(i => new ExternalTaskDto
        {
            Id = i.Key,
            Title = i.Fields?.Summary ?? string.Empty,
            Status = i.Fields?.Status?.Name,
            Url = $"{baseUrl.TrimEnd('/')}/browse/{i.Key}",
            ProjectId = i.Fields?.Project?.Id ?? projectId,
            Provider = ExternalProvider.Jira
        })];
    }

    private HttpClient CreateClient(string accessToken, string baseUrl)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");

        var base64Auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(accessToken));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Auth);

        return client;
    }
}
