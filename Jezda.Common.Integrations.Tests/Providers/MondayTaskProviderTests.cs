using System.Net;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Monday.Providers;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Providers;

public class MondayTaskProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly MondayTaskProvider _provider;

    public MondayTaskProviderTests()
    {
        var factory = new MockHttpClientFactory(
            _handler,
            MondayTaskProvider.HttpClientName,
            new Uri("https://api.monday.com/v2"));

        _provider = new MondayTaskProvider(factory, NullLogger<MondayTaskProvider>.Instance);
    }

    [Fact]
    public void Provider_ReturnsMonday()
    {
        Assert.Equal(ExternalProvider.Monday, _provider.Provider);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithValidToken_ReturnsTrue()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { data = new { me = new { id = "user1" } } });

        var result = await _provider.ValidateConnectionAsync("monday-api-token");

        Assert.True(result);
        Assert.Single(_handler.SentRequests);
        Assert.Equal("Bearer", _handler.SentRequests[0].Headers.Authorization?.Scheme);
        Assert.Equal("monday-api-token", _handler.SentRequests[0].Headers.Authorization?.Parameter);
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
        var response = new
        {
            data = new
            {
                boards = new[]
                {
                    new { id = "123", name = "Sprint Board", description = "Sprint planning" },
                    new { id = "456", name = "Backlog", description = "" }
                }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, response);

        var result = await _provider.GetProjectsAsync("monday-api-token");

        Assert.Equal(2, result.Count);
        Assert.Equal("123", result[0].Id);
        Assert.Equal("Sprint Board", result[0].Name);
        Assert.Equal("Sprint planning", result[0].Description);
        Assert.Equal(ExternalProvider.Monday, result[0].Provider);
    }

    [Fact]
    public async Task GetTasksAsync_ReturnsExternalTaskDtos()
    {
        var response = new
        {
            data = new
            {
                boards = new[]
                {
                    new
                    {
                        items_page = new
                        {
                            items = new[]
                            {
                                new { id = "item1", name = "Design mockup", state = "active" },
                                new { id = "item2", name = "Write tests", state = "active" }
                            },
                            cursor = (string?)null
                        }
                    }
                }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, response);

        var result = await _provider.GetTasksAsync("123", "monday-api-token");

        Assert.Equal(2, result.Count);
        Assert.Equal("item1", result[0].Id);
        Assert.Equal("Design mockup", result[0].Title);
        Assert.Equal("active", result[0].Status);
        Assert.Equal("123", result[0].ProjectId);
        Assert.Equal(ExternalProvider.Monday, result[0].Provider);
    }
}
