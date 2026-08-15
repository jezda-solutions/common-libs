using System.Net;
using System.Text.Json;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.AzureDevOps.Configuration;
using Jezda.Common.Integrations.AzureDevOps.Providers;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Providers;

public class AzureDevOpsTaskProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly AzureDevOpsTaskProvider _provider;

    public AzureDevOpsTaskProviderTests()
    {
        var factory = new MockHttpClientFactory(_handler, AzureDevOpsTaskProvider.HttpClientName);
        var options = Options.Create(new AzureDevOpsOptions { ApiVersion = "7.1" });
        _provider = new AzureDevOpsTaskProvider(factory, NullLogger<AzureDevOpsTaskProvider>.Instance, options);
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

        var result = await _provider.GetTasksAsync("my-pat", "ProjectA", "https://dev.azure.com/myorg/");

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

        var result = await _provider.GetTasksAsync("my-pat", "ProjectA", "https://dev.azure.com/myorg/");

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchTasksAsync_QueriesTheWholeOrganisation()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { queryType = "flat", workItems = Array.Empty<object>() });

        await _provider.SearchTasksAsync("my-pat", "webhook", baseUrl: "https://dev.azure.com/myorg/");

        // No project segment in the path and no [System.TeamProject] in the WHERE clause: that is
        // what makes this organisation-wide rather than one query per project. The "myorg" segment
        // is the organisation, and comes from the base address — GetTasksAsync would add a project
        // segment after it.
        var request = Assert.Single(_handler.SentRequests);
        Assert.Equal("/myorg/_apis/wit/wiql", request.RequestUri!.AbsolutePath);

        var wiql = await ReadWiqlQueryAsync(request);
        Assert.Contains("[System.Title] CONTAINS 'webhook'", wiql);
        Assert.Contains("[System.State] <> 'Removed'", wiql);
        Assert.DoesNotContain("System.TeamProject", wiql);
    }

    [Fact]
    public async Task SearchTasksAsync_ReadsProjectFromTheWorkItemNotAParameter()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            queryType = "flat",
            workItems = new object[] { new { id = 7, url = "https://dev.azure.com/myorg/_apis/wit/workItems/7" } }
        });
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            count = 1,
            value = new object[]
            {
                new
                {
                    id = 7,
                    url = "https://dev.azure.com/myorg/_apis/wit/workItems/7",
                    fields = new Dictionary<string, object>
                    {
                        ["System.Title"] = "Fix webhook retry",
                        ["System.State"] = "Active",
                        ["System.TeamProject"] = "Platform"
                    }
                }
            }
        });

        var result = await _provider.SearchTasksAsync("my-pat", "webhook", baseUrl: "https://dev.azure.com/myorg/");

        var task = Assert.Single(result);
        Assert.Equal("7", task.Id);
        Assert.Equal("Fix webhook retry", task.Title);
        Assert.Equal("Active", task.Status);
        Assert.Equal("Platform", task.ProjectId);
        Assert.Equal(ExternalProvider.AzureDevOps, task.Provider);
    }

    [Fact]
    public async Task SearchTasksAsync_TrimsIdsBeforeFetchingDetails()
    {
        var workItems = Enumerable.Range(1, 10)
            .Select(n => (object)new { id = n, url = $"https://dev.azure.com/myorg/_apis/wit/workItems/{n}" })
            .ToArray();
        _handler.EnqueueResponse(HttpStatusCode.OK, new { queryType = "flat", workItems });
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 0, value = Array.Empty<object>() });

        await _provider.SearchTasksAsync("my-pat", "webhook", limit: 3, baseUrl: "https://dev.azure.com/myorg/");

        // The details call is the expensive one — it must not hydrate rows the caller discards.
        var detailsQuery = _handler.SentRequests[1].RequestUri!.Query;
        Assert.Contains("ids=1,2,3", Uri.UnescapeDataString(detailsQuery));
    }

    [Fact]
    public async Task SearchTasksAsync_EscapesQuotesInTheSearchTerm()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { queryType = "flat", workItems = Array.Empty<object>() });

        await _provider.SearchTasksAsync("my-pat", "it's broken", baseUrl: "https://dev.azure.com/myorg/");

        var wiql = await ReadWiqlQueryAsync(_handler.SentRequests[0]);
        Assert.Contains("CONTAINS 'it''s broken'", wiql);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchTasksAsync_BlankTerm_ReturnsEmptyWithoutQuerying(string term)
    {
        var result = await _provider.SearchTasksAsync("my-pat", term, baseUrl: "https://dev.azure.com/myorg/");

        Assert.Empty(result);
        Assert.Empty(_handler.SentRequests);
    }

    /// <summary>
    /// Pulls the WIQL out of the request body. Asserting on the raw JSON does not work: the default
    /// <c>JavaScriptEncoder</c> escapes <c>'</c> and <c>&lt;&gt;</c> to <c>'</c> / <c><</c>,
    /// so the query reads nothing like what Azure DevOps receives after parsing.
    /// </summary>
    private static async Task<string> ReadWiqlQueryAsync(HttpRequestMessage request)
    {
        var body = await request.Content!.ReadAsStringAsync();

        return JsonDocument.Parse(body).RootElement.GetProperty("query").GetString()!;
    }
}
