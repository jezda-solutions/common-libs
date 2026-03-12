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
            Id = p.Key,
            Name = p.Name,
            Description = null,
            Url = $"{baseUrl.TrimEnd('/')}/browse/{p.Key}",
            Provider = ExternalProvider.Jira
        })];
    }

    public async Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(string accessToken, string projectId, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl, nameof(baseUrl));

        using var client = CreateClient(accessToken, baseUrl);

        var searchRequest = new JiraSearchRequest
        {
            Jql = $"project = \"{projectId.Replace("\"", "\\\"")}\"",
            StartAt = 0,
            MaxResults = 50,
            Fields = ["summary", "status"]
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
            ProjectId = projectId,
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
