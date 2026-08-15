using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Providers;

/// <summary>
/// Covers the default implementation of <see cref="IExternalTaskProvider.SearchTasksAsync"/> — the
/// one every provider without a native search inherits (Jira, Trello, ClickUp, Monday).
/// </summary>
public class FallbackSearchTests
{
    [Fact]
    public async Task FansOutOverProjectsAndMatchesTitleCaseInsensitively()
    {
        IExternalTaskProvider provider = new FakeProvider(
            projects: ["alpha", "beta"],
            tasksByProject: new()
            {
                ["alpha"] = [("1", "Fix WEBHOOK retry"), ("2", "Unrelated")],
                ["beta"] = [("3", "webhook cleanup")]
            });

        var result = await provider.SearchTasksAsync("token", "webhook");

        Assert.Equal(2, result.Count);
        Assert.Equal(["1", "3"], result.Select(t => t.Id));
        // ProjectId survives the fan-out — it is what a consumer keys a stored row on.
        Assert.Equal("alpha", result[0].ProjectId);
        Assert.Equal("beta", result[1].ProjectId);
    }

    [Fact]
    public async Task MatchesOnExternalIdAsWellAsTitle()
    {
        IExternalTaskProvider provider = new FakeProvider(
            projects: ["alpha"],
            tasksByProject: new() { ["alpha"] = [("412", "Nothing to do with the term")] });

        var result = await provider.SearchTasksAsync("token", "412");

        Assert.Single(result);
    }

    [Fact]
    public async Task StopsCallingProjectsOnceTheLimitIsReached()
    {
        var provider = new FakeProvider(
            projects: ["alpha", "beta", "gamma"],
            tasksByProject: new()
            {
                ["alpha"] = [("1", "match"), ("2", "match")],
                ["beta"] = [("3", "match")],
                ["gamma"] = [("4", "match")]
            });

        var result = await ((IExternalTaskProvider)provider).SearchTasksAsync("token", "match", limit: 2);

        Assert.Equal(2, result.Count);
        // Every extra project is another HTTP round trip nobody is waiting for: after "alpha"
        // filled the limit, "beta" and "gamma" must not be fetched.
        Assert.Equal(["alpha"], provider.RequestedProjects);
    }

    [Fact]
    public async Task TrimsOverflowFromTheLastProjectToTheLimit()
    {
        IExternalTaskProvider provider = new FakeProvider(
            projects: ["alpha"],
            tasksByProject: new() { ["alpha"] = [("1", "match"), ("2", "match"), ("3", "match")] });

        var result = await provider.SearchTasksAsync("token", "match", limit: 2);

        Assert.Equal(2, result.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankTerm_ReturnsEmptyWithoutTouchingTheProvider(string term)
    {
        var provider = new FakeProvider(projects: ["alpha"], tasksByProject: new() { ["alpha"] = [("1", "anything")] });

        var result = await ((IExternalTaskProvider)provider).SearchTasksAsync("token", term);

        Assert.Empty(result);
        Assert.False(provider.ProjectsRequested);
    }

    [Fact]
    public async Task NonPositiveLimit_ReturnsEmptyWithoutTouchingTheProvider()
    {
        var provider = new FakeProvider(projects: ["alpha"], tasksByProject: new() { ["alpha"] = [("1", "match")] });

        var result = await ((IExternalTaskProvider)provider).SearchTasksAsync("token", "match", limit: 0);

        Assert.Empty(result);
        Assert.False(provider.ProjectsRequested);
    }

    /// <summary>
    /// Implements only the three original members, so <c>SearchTasksAsync</c> resolves to the
    /// interface's default body — which is exactly what is under test.
    /// </summary>
    private sealed class FakeProvider(
        List<string> projects,
        Dictionary<string, List<(string Id, string Title)>> tasksByProject) : IExternalTaskProvider
    {
        public List<string> RequestedProjects { get; } = [];

        public bool ProjectsRequested { get; private set; }

        public ExternalProvider Provider => ExternalProvider.Jira;

        public Task<bool> ValidateConnectionAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
        {
            ProjectsRequested = true;

            return Task.FromResult<IReadOnlyList<ExternalProjectDto>>(
                [.. projects.Select(p => new ExternalProjectDto { Id = p, Name = p, Provider = ExternalProvider.Jira })]);
        }

        public Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(string accessToken, string projectId, string? baseUrl = null, CancellationToken cancellationToken = default)
        {
            RequestedProjects.Add(projectId);

            var tasks = tasksByProject.TryGetValue(projectId, out var t) ? t : [];

            return Task.FromResult<IReadOnlyList<ExternalTaskDto>>(
                [.. tasks.Select(x => new ExternalTaskDto
                {
                    Id = x.Id,
                    Title = x.Title,
                    ProjectId = projectId,
                    Provider = ExternalProvider.Jira
                })]);
        }
    }
}
