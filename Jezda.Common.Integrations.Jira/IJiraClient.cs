using Jezda.Common.Integrations.Jira.Models;

namespace Jezda.Common.Integrations.Jira;

public interface IJiraClient
{
    /// <summary>
    /// Returns all projects visible to the user.
    /// </summary>
    Task<List<JiraProject>> GetProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one page of issues matching a JQL (Jira Query Language) query, through
    /// <c>POST rest/api/3/search/jql</c>.
    /// </summary>
    /// <param name="jql">The query. Jira rejects one without a restriction such as <c>project = X</c>.</param>
    /// <param name="nextPageToken">
    /// Null for the first page, then <see cref="JiraSearchResponse.NextPageToken"/> of the previous
    /// page. Jira removed the offset-based <c>rest/api/3/search</c> (it answers 410 Gone).
    /// </param>
    /// <param name="maxResults">Page size; Jira caps it and may return fewer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<JiraSearchResponse?> SearchIssuesAsync(string jql, string? nextPageToken = null, int maxResults = 50, CancellationToken cancellationToken = default);
}
