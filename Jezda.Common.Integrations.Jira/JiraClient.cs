using System.Net.Http.Headers;
using System.Text;
using Jezda.Common.Integrations.Clients;
using Jezda.Common.Integrations.Jira.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jezda.Common.Integrations.Jira;

public class JiraClient : BaseIntegrationClient, IJiraClient
{
    private readonly JiraOptions _options;

    public JiraClient(
        HttpClient httpClient,
        IOptions<JiraOptions> options,
        ILogger<JiraClient> logger)
        : base(httpClient, logger)
    {
        _options = options.Value;

        HttpClient.BaseAddress = new Uri(_options.BaseUrl);
        
        // Jira Cloud requires Basic Auth (Email + API Token) base64 encoded
        var authString = $"{_options.Email}:{_options.ApiToken}";
        var base64Auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(authString));
        
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Auth);
        HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<JiraProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        // Endpoint: /rest/api/3/project
        return await GetAsync<List<JiraProject>>("rest/api/3/project", cancellationToken) ?? new List<JiraProject>();
    }

    public async Task<JiraSearchResponse?> SearchIssuesAsync(string jql, int startAt = 0, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        // Endpoint: /rest/api/3/search
        // Using POST for search to handle long JQL strings
        var request = new
        {
            jql,
            startAt,
            maxResults,
            fields = new[] { "summary", "status", "project", "priority" }
        };

        return await PostAsync<object, JiraSearchResponse>("rest/api/3/search", request, cancellationToken);
    }
}
