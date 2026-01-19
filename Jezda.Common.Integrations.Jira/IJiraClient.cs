using Jezda.Common.Integrations.Jira.Models;

namespace Jezda.Common.Integrations.Jira;

public interface IJiraClient
{
    /// <summary>
    /// Returns all projects visible to the user.
    /// </summary>
    Task<List<JiraProject>> GetProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for issues using JQL (Jira Query Language).
    /// </summary>
    Task<JiraSearchResponse?> SearchIssuesAsync(string jql, int startAt = 0, int maxResults = 50, CancellationToken cancellationToken = default);
}
