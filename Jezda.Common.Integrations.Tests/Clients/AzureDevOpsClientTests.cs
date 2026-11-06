using System.Net;
using Jezda.Common.Integrations.AzureDevOps;
using Jezda.Common.Integrations.AzureDevOps.Configuration;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Clients;

public class AzureDevOpsClientTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly AzureDevOpsClient _client;

    public AzureDevOpsClientTests()
    {
        var httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://dev.azure.com/myorg/") };
        var options = Options.Create(new AzureDevOpsOptions { ApiVersion = "7.1" });
        _client = new AzureDevOpsClient(httpClient, options, NullLogger<AzureDevOpsClient>.Instance);
    }

    [Fact]
    public async Task GetWorkItemsByQueryAsync_AsksTheDetailsBatchToOmitUnreadableItems()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1, 2));
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":2,"value":[{"id":1,"fields":{}},{"id":2,"fields":{}}]}""");

        await _client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems");

        // Without it, one id deleted between the WIQL and this call fails the whole batch with 404.
        var detailsQuery = _handler.SentRequests[1].RequestUri!.Query;
        Assert.Contains("errorPolicy=omit", detailsQuery);
    }

    [Fact]
    public async Task GetWorkItemsByQueryAsync_SkipsItemsTheDetailsBatchOmitted()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1, 2, 3));
        // With errorPolicy=omit, Azure DevOps answers an unreadable id with a null slot.
        _handler.EnqueueResponse(
            HttpStatusCode.OK,
            """{"count":3,"value":[{"id":1,"fields":{}},null,{"id":3,"fields":{}}]}""");

        var result = await _client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems");

        Assert.Equal([1, 3], result.Select(w => w.Id));
    }

    [Fact]
    public async Task GetWorkItemsByQueryAsync_NoMatches_SkipsTheDetailsBatch()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs());

        var result = await _client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems");

        Assert.Empty(result);
        Assert.Single(_handler.SentRequests);
    }

    private static string Refs(params int[] ids) =>
        $$"""{"workItems":[{{string.Join(",", ids.Select(id => $$"""{"id":{{id}}}"""))}}]}""";
}
