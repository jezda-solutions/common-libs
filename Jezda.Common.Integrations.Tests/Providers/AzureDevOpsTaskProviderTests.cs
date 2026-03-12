using System.Net;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.AzureDevOps.Providers;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Providers;

public class AzureDevOpsTaskProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly AzureDevOpsTaskProvider _provider;

    public AzureDevOpsTaskProviderTests()
    {
        var factory = new MockHttpClientFactory(_handler, AzureDevOpsTaskProvider.HttpClientName);
        _provider = new AzureDevOpsTaskProvider(factory, NullLogger<AzureDevOpsTaskProvider>.Instance);
    }

    [Fact]
    public void Provider_ReturnsAzureDevOps()
    {
        Assert.Equal(ExternalProvider.AzureDevOps, _provider.Provider);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithValidToken_ReturnsTrue()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 1, value = new[] { new { id = "proj-1", name = "Project" } } });

        var result = await _provider.ValidateConnectionAsync("my-pat", "https://dev.azure.com/myorg/");

        Assert.True(result);
        Assert.Single(_handler.SentRequests);
        Assert.Equal("Basic", _handler.SentRequests[0].Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithInvalidToken_ReturnsFalse()
    {
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        var result = await _provider.ValidateConnectionAsync("bad-pat", "https://dev.azure.com/myorg/");

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithoutBaseUrl_ThrowsArgumentException()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            _provider.ValidateConnectionAsync("pat", baseUrl: null));
    }

    [Fact]
    public async Task GetProjectsAsync_ReturnsExternalProjectDtos()
    {
        var response = new
        {
            count = 2,
            value = new[]
            {
                new { id = "guid-1", name = "ProjectA", description = "First project", url = "https://dev.azure.com/myorg/ProjectA", state = "wellFormed", revision = 1L, visibility = "private", lastUpdateTime = "2025-01-01T00:00:00Z" },
                new { id = "guid-2", name = "ProjectB", description = "", url = "https://dev.azure.com/myorg/ProjectB", state = "wellFormed", revision = 2L, visibility = "public", lastUpdateTime = "2025-01-02T00:00:00Z" }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, response);

        var result = await _provider.GetProjectsAsync("my-pat", "https://dev.azure.com/myorg/");

        Assert.Equal(2, result.Count);
        Assert.Equal("ProjectA", result[0].Id);
        Assert.Equal("ProjectA", result[0].Name);
        Assert.Equal("First project", result[0].Description);
        Assert.Equal(ExternalProvider.AzureDevOps, result[0].Provider);
    }

    [Fact]
    public async Task GetTasksAsync_ReturnsExternalTaskDtos()
    {
        // WIQL response with work item IDs
        var wiqlResponse = new
        {
            queryType = "flat",
            workItems = new[]
            {
                new { id = 1, url = "https://dev.azure.com/myorg/_apis/wit/workitems/1" },
                new { id = 2, url = "https://dev.azure.com/myorg/_apis/wit/workitems/2" }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, wiqlResponse);

        // Batch work item details
        var workItemsResponse = new
        {
            count = 2,
            value = new[]
            {
                new
                {
                    id = 1,
                    url = "https://dev.azure.com/myorg/_apis/wit/workitems/1",
                    fields = new Dictionary<string, object>
                    {
                        ["System.Title"] = "Task One",
                        ["System.State"] = "Active"
                    }
                },
                new
                {
                    id = 2,
                    url = "https://dev.azure.com/myorg/_apis/wit/workitems/2",
                    fields = new Dictionary<string, object>
                    {
                        ["System.Title"] = "Task Two",
                        ["System.State"] = "New"
                    }
                }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, workItemsResponse);

        var result = await _provider.GetTasksAsync("ProjectA", "my-pat", "https://dev.azure.com/myorg/");

        Assert.Equal(2, result.Count);
        Assert.Equal("1", result[0].Id);
        Assert.Equal("Task One", result[0].Title);
        Assert.Equal("Active", result[0].Status);
        Assert.Equal("ProjectA", result[0].ProjectId);
        Assert.Equal(ExternalProvider.AzureDevOps, result[0].Provider);
    }

    [Fact]
    public async Task GetTasksAsync_EmptyWiqlResult_ReturnsEmpty()
    {
        var wiqlResponse = new { queryType = "flat", workItems = Array.Empty<object>() };
        _handler.EnqueueResponse(HttpStatusCode.OK, wiqlResponse);

        var result = await _provider.GetTasksAsync("ProjectA", "my-pat", "https://dev.azure.com/myorg/");

        Assert.Empty(result);
    }
}
