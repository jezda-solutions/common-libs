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

    [Theory]
    [InlineData("token\u201D")] // A pasted smart quote
    [InlineData("tok\u00E9n")]
    [InlineData("token\n")] // A pasted line break
    [InlineData("token\r\n")]
    public async Task ValidateConnectionAsync_TokenThatCannotGoInAHeader_ReturnsFalseWithoutSending(string token)
    {
        var result = await _provider.ValidateConnectionAsync(token);

        Assert.False(result);
        Assert.Empty(_handler.SentRequests);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithInvalidToken_ReturnsFalse()
    {
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        var result = await _provider.ValidateConnectionAsync("bad-token");

        Assert.False(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ValidateConnectionAsync_OtherFailureStatus_ThrowsInsteadOfBlamingTheToken(HttpStatusCode status)
    {
        _handler.EnqueueResponse(status);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.ValidateConnectionAsync("token"));

        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task ValidateConnectionAsync_ConnectionFailure_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueException(new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<HttpRequestException>(() => _provider.ValidateConnectionAsync("token"));
    }

    [Fact]
    public async Task ValidateConnectionAsync_Timeout_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueException(new TaskCanceledException("The request timed out"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _provider.ValidateConnectionAsync("token"));
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
        Assert.Equal("team1", result[0].Path);
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
