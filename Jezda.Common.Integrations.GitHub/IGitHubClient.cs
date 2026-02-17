using Jezda.Common.Integrations.GitHub.Models;

namespace Jezda.Common.Integrations.GitHub;

public interface IGitHubClient
{
    /// <summary>
    /// Gets all repositories for the authenticated user.
    /// </summary>
    Task<List<GitHubRepository>> GetRepositoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets issues for a specific repository.
    /// </summary>
    Task<List<GitHubIssue>> GetIssuesAsync(string owner, string repo, CancellationToken cancellationToken = default);
}
