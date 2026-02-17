using Jezda.Common.Integrations.Clients;
using Jezda.Common.Integrations.GitHub.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jezda.Common.Integrations.GitHub;

public class GitHubClient(
    HttpClient httpClient,
    IOptions<GitHubOptions> options,
    ILogger<GitHubClient> logger) : BaseIntegrationClient(httpClient, logger), IGitHubClient
{
    private readonly GitHubOptions _options = options.Value;

    public async Task<List<GitHubRepository>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        return await GetAsync<List<GitHubRepository>>("user/repos", cancellationToken) ?? new List<GitHubRepository>();
    }

    public async Task<List<GitHubIssue>> GetIssuesAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        var url = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/issues";
        return await GetAsync<List<GitHubIssue>>(url, cancellationToken) ?? new List<GitHubIssue>();
    }
}
