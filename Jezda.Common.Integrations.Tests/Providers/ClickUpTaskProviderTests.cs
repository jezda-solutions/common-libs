using System.Net;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.ClickUp.Providers;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Providers;

public class ClickUpTaskProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly ClickUpTaskProvider _provider;

    public ClickUpTaskProviderTests()
    {
        var factory = new MockHttpClientFactory(
            _handler,
            ClickUpTaskProvider.HttpClientName,
            new Uri("https://api.clickup.com/api/v2/"));

        _provider = new ClickUpTaskProvider(factory, NullLogger<ClickUpTaskProvider>.Instance);
    }

    [Fact]
    public void Provider_ReturnsClickUp()
    {
        Assert.Equal(ExternalProvider.ClickUp, _provider.Provider);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithValidToken_ReturnsTrue()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { teams = new[] { new { id = "team1", name = "My Team" } } });

        var result = await _provider.ValidateConnectionAsync("pk_test_token");

        Assert.True(result);
        Assert.Single(_handler.SentRequests);
        Assert.Equal("Bearer", _handler.SentRequests[0].Headers.Authorization?.Scheme);
        Assert.Equal("pk_test_token", _handler.SentRequests[0].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithInvalidToken_ReturnsFalse()
    {
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        var result = await _provider.ValidateConnectionAsync("bad-token");

        Assert.False(result);
    }

    [Fact]
    public async Task GetProjectsAsync_ReturnsExternalProjectDtos()
    {
        var teamsResponse = new
        {
            teams = new[]
            {
                new { id = "team1", name = "Engineering", color = "#FF0000" },
                new { id = "team2", name = "Design", color = "#00FF00" }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, teamsResponse);

        var result = await _provider.GetProjectsAsync("pk_test_token");

        Assert.Equal(2, result.Count);
        Assert.Equal("team1", result[0].Id);
        Assert.Equal("Engineering", result[0].Name);
        Assert.Equal(ExternalProvider.ClickUp, result[0].Provider);
    }

    [Fact]
    public async Task GetTasksAsync_ReturnsExternalTaskDtos()
    {
        var tasksResponse = new
        {
            tasks = new[]
            {
                new { id = "task1", name = "Implement feature", status = new { status = "in progress" }, url = "https://app.clickup.com/t/task1", space = new { id = "space1" } },
                new { id = "task2", name = "Fix bug", status = new { status = "open" }, url = "https://app.clickup.com/t/task2", space = new { id = "space1" } }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, tasksResponse);

        var result = await _provider.GetTasksAsync("pk_test_token", "team1");

        Assert.Equal(2, result.Count);
        Assert.Equal("task1", result[0].Id);
        Assert.Equal("Implement feature", result[0].Title);
        Assert.Equal("in progress", result[0].Status);
        Assert.Equal("team1", result[0].ProjectId);
        Assert.Equal(ExternalProvider.ClickUp, result[0].Provider);
    }
}
