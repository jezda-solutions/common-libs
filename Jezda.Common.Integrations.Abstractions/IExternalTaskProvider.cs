using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;

namespace Jezda.Common.Integrations.Abstractions;

public interface IExternalTaskProvider
{
    ExternalProvider Provider { get; }

    /// <summary>
    /// Asks the provider whether it accepts the token.
    /// </summary>
    /// <remarks>
    /// What counts as a rejected token differs per provider:
    /// <list type="bullet">
    /// <item><description>Azure DevOps: 401, 403 or a sign-in page.</description></item>
    /// <item><description>GitHub: 401, or 403 that is not a rate limit (<c>x-ratelimit-remaining: 0</c>, <c>retry-after</c>, or a rate limit message).</description></item>
    /// <item><description>Jira: 401 or 403, unless <c>X-Seraph-LoginReason</c> is <c>AUTHENTICATION_DENIED</c>: the CAPTCHA was triggered and needs a sign-in through the website.</description></item>
    /// <item><description>ClickUp: 401.</description></item>
    /// <item><description>Monday.com: 401 or 403, or a rejection code in a GraphQL error on status 200. An IP restriction or a temporary API block is not a rejection.</description></item>
    /// <item><description>Trello: 401, for a bad key as well as a bad token.</description></item>
    /// </list>
    /// </remarks>
    /// <returns><c>true</c> if the token is accepted, <c>false</c> only if the provider rejected it.</returns>
    /// <exception cref="HttpRequestException">
    /// Any other failure, so an outage or a rate limit does not read as a bad token. Handle it as
    /// "could not check". The resilience pipeline can also throw Polly's
    /// <c>TimeoutRejectedException</c> or <c>BrokenCircuitException</c>, which do not derive from it.
    /// </exception>
    Task<bool> ValidateConnectionAsync(
        string accessToken,
        string? baseUrl = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(
        string accessToken,
        string? baseUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the tasks of one project.
    /// </summary>
    /// <param name="accessToken">The provider token.</param>
    /// <param name="projectId">
    /// The project's <see cref="ExternalProjectDto.Id"/>, which keeps working after a rename. Its
    /// current <see cref="ExternalProjectDto.Path"/> is accepted too, so a row stored as a path keeps
    /// working until the project is renamed; after that the call can fail, so migrate such rows to
    /// the id. Whichever was passed, <see cref="ExternalTaskDto.ProjectId"/> is the durable id.
    /// </param>
    /// <param name="baseUrl">The organisation or site address, for providers that need one.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(
        string accessToken,
        string projectId,
        string? baseUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds tasks matching <paramref name="searchTerm"/> without enumerating every project.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists for interactive lookups — a user typing into a task picker — where
    /// <see cref="GetProjectsAsync"/> followed by <see cref="GetTasksAsync"/> per project is one
    /// round trip per project and far too slow to sit behind a keystroke.
    /// </para>
    /// <para>
    /// <b>The default implementation is that slow fan-out.</b> It keeps implementors that have
    /// nothing better compiling and working, but it costs <c>1 + N</c> requests and filters in
    /// memory. Any provider whose API can search server-side should override it;
    /// <c>GitHubTaskProvider</c> and <c>AzureDevOpsTaskProvider</c> do.
    /// </para>
    /// <para>
    /// <b>The fallback is best-effort, not exhaustive.</b> It can only see what
    /// <see cref="GetTasksAsync"/> returns, and several providers return a truncated first page
    /// there — Jira caps at 50 issues per project with no <c>startAt</c> loop, ClickUp and Monday
    /// take their API defaults. A task past that cut is simply not found, and the caller cannot
    /// tell that apart from "no such task". Overriding with a native search is the fix; paging
    /// <see cref="GetTasksAsync"/> is the other.
    /// </para>
    /// <para>
    /// Matching in the fallback is case-insensitive on <see cref="ExternalTaskDto.Title"/> and
    /// <see cref="ExternalTaskDto.Id"/>. An override may match more (labels, description,
    /// assignee), and may match less where the provider's API leaves no choice — GitHub scopes to
    /// <c>involves:@me</c>, Azure DevOps matches title only. A narrower scope must be documented on
    /// the override, because the picker then behaves differently per provider.
    /// </para>
    /// </remarks>
    /// <param name="searchTerm">
    /// The user's text. Callers are expected to have trimmed it and rejected the empty case; an
    /// implementation given whitespace returns an empty list rather than every task in the account.
    /// </param>
    /// <param name="limit">Maximum number of results to return. Implementations may return fewer.</param>
    Task<IReadOnlyList<ExternalTaskDto>> SearchTasksAsync(
        string accessToken,
        string searchTerm,
        int limit = 20,
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
        => SearchByFanOutAsync(this, accessToken, searchTerm, limit, baseUrl, cancellationToken);

    /// <summary>
    /// The fallback search: list every project, list every task in each, filter in memory.
    /// </summary>
    /// <remarks>
    /// A static helper rather than inline in the default body so an override can still reach it —
    /// a provider whose native search covers only part of its surface can fall back to this for the
    /// rest without reimplementing it.
    /// </remarks>
    static async Task<IReadOnlyList<ExternalTaskDto>> SearchByFanOutAsync(
        IExternalTaskProvider provider,
        string accessToken,
        string searchTerm,
        int limit,
        string? baseUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || limit <= 0)
        {
            return [];
        }

        var term = searchTerm.Trim();
        var projects = await provider.GetProjectsAsync(accessToken, baseUrl, cancellationToken);
        var matches = new List<ExternalTaskDto>();

        foreach (var project in projects)
        {
            // Stop calling the provider once we have enough: the caller asked for `limit` results,
            // and each extra project is another HTTP round trip nobody is waiting for.
            if (matches.Count >= limit)
            {
                break;
            }

            var tasks = await provider.GetTasksAsync(accessToken, project.Id, baseUrl, cancellationToken);

            matches.AddRange(tasks.Where(t =>
                t.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || t.Id.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        return matches.Count > limit ? matches[..limit] : matches;
    }
}
