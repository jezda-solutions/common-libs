using Jezda.Common.Integrations.Clients;
using Jezda.Common.Integrations.Jira.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jezda.Common.Integrations.Jira;

public class JiraClient(
    HttpClient httpClient,
    IOptions<JiraOptions> options,
    ILogger<JiraClient> logger) : BaseIntegrationClient(httpClient, logger), IJiraClient
{
    private readonly JiraOptions _options = options.Value;

    public async Task<List<JiraProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        return await GetAsync<List<JiraProject>>("rest/api/3/project", cancellationToken) ?? new List<JiraProject>();
    }

    public async Task<JiraSearchResponse?> SearchIssuesAsync(string jql, int startAt = 0, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        var request = new JiraSearchRequest
        {
            Jql = jql,
            StartAt = startAt,
            MaxResults = maxResults,
            Fields = ["summary", "status", "project", "priority"]
        };

        return await PostAsync<JiraSearchRequest, JiraSearchResponse>("rest/api/3/search", request, cancellationToken);
    }
}
