using System.Net.Http.Headers;
using Jezda.Common.Integrations.Clients;
using Jezda.Common.Integrations.GitHub.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jezda.Common.Integrations.GitHub;

public class GitHubClient : BaseIntegrationClient, IGitHubClient
{
    private readonly GitHubOptions _options;

    public GitHubClient(
        HttpClient httpClient,
        IOptions<GitHubOptions> options,
        ILogger<GitHubClient> logger) 
        : base(httpClient, logger)
    {
        _options = options.Value;

        // Configure HttpClient
        HttpClient.BaseAddress = new Uri(_options.BaseUrl);
        HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        HttpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
    }

    public async Task<List<GitHubRepository>> GetRepositoriesAsync(CancellationToken cancellationToken = default)
    {
        // Get user repos (paged, but for simplicity getting first page or default list)
        // Endpoint: /user/repos (lists repos that the user has access to)
        return await GetAsync<List<GitHubRepository>>("user/repos", cancellationToken) ?? new List<GitHubRepository>();
    }

    public async Task<List<GitHubIssue>> GetIssuesAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        // Endpoint: /repos/{owner}/{repo}/issues
        var url = $"repos/{owner}/{repo}/issues";
        return await GetAsync<List<GitHubIssue>>(url, cancellationToken) ?? new List<GitHubIssue>();
    }
}
