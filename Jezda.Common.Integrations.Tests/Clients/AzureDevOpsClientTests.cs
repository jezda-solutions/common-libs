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
    public async Task GetWorkItemsByQueryAsync_ItemsOmitted_LogsAWarningWithTheCounts()
    {
        var logger = new RecordingLogger<AzureDevOpsClient>();
        var client = new AzureDevOpsClient(
            new HttpClient(_handler) { BaseAddress = new Uri("https://dev.azure.com/myorg/") },
            Options.Create(new AzureDevOpsOptions { ApiVersion = "7.1" }),
            logger);
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1, 2, 3));
        _handler.EnqueueResponse(
            HttpStatusCode.OK,
            """{"count":3,"value":[{"id":1,"fields":{}},null,{"id":3,"fields":{}}]}""");

        await client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems");

        var warning = Assert.Single(logger.Warnings);
        Assert.Contains("returned 2 of 3", warning);
        Assert.Contains("https://dev.azure.com/myorg/", warning);
        Assert.EndsWith("Missing ids: 2", warning);
    }

    [Fact]
    public async Task GetWorkItemsByQueryAsync_ManyItemsOmitted_CapsTheIdsInTheWarning()
    {
        var logger = new RecordingLogger<AzureDevOpsClient>();
        var client = new AzureDevOpsClient(
            new HttpClient(_handler) { BaseAddress = new Uri("https://dev.azure.com/myorg/") },
            Options.Create(new AzureDevOpsOptions { ApiVersion = "7.1" }),
            logger);
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs([.. Enumerable.Range(1, 25)]));
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":0,"value":[]}""");

        await client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems");

        var warning = Assert.Single(logger.Warnings);
        Assert.Contains("returned 0 of 25", warning);
        Assert.EndsWith("Missing ids: 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 and 5 more", warning);
    }

    [Fact]
    public async Task GetWorkItemsByQueryAsync_ItemsOmitted_KeepsBaseUrlCredentialsOutOfTheWarning()
    {
        var logger = new RecordingLogger<AzureDevOpsClient>();
        var client = new AzureDevOpsClient(
            new HttpClient(_handler) { BaseAddress = new Uri("https://admin:base-secret@dev.azure.com/myorg/") },
            Options.Create(new AzureDevOpsOptions { ApiVersion = "7.1" }),
            logger);
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1));
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":1,"value":[null]}""");

        await client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems");

        Assert.DoesNotContain("secret", Assert.Single(logger.Warnings));
    }

    [Fact]
    public async Task GetWorkItemsByQueryAsync_NothingOmitted_LogsNoWarning()
    {
        var logger = new RecordingLogger<AzureDevOpsClient>();
        var client = new AzureDevOpsClient(
            new HttpClient(_handler) { BaseAddress = new Uri("https://dev.azure.com/myorg/") },
            Options.Create(new AzureDevOpsOptions { ApiVersion = "7.1" }),
            logger);
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1));
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":1,"value":[{"id":1,"fields":{}}]}""");

        await client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems");

        Assert.Empty(logger.Warnings);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("""{"count":2}""")]
    [InlineData("""{"count":2,"value":null}""")]
    public async Task GetWorkItemsByQueryAsync_BatchAnswersWithoutAList_ThrowsInsteadOfReadingAsOmitted(string body)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1, 2));
        _handler.EnqueueResponse(HttpStatusCode.OK, body);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => _client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems"));
    }

    [Fact]
    public async Task GetWorkItemsByQueryAsync_BatchAnswersNoContent_ThrowsInsteadOfReadingAsOmitted()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1, 2));
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.NoContent));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => _client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems"));
    }

    [Fact]
    public async Task GetWorkItemsByQueryAsync_BatchAnswersAnEmptyList_ReadsAsOmitted()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1, 2));
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":0,"value":[]}""");

        var result = await _client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetWorkItemsByQueryAsync_MoreIdsThanOneBatch_SplitsAt200()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs([.. Enumerable.Range(1, 201)]));
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":1,"value":[{"id":1,"fields":{}}]}""");
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":1,"value":[{"id":201,"fields":{}}]}""");

        var result = await _client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems");

        Assert.Equal([1, 201], result.Select(w => w.Id));
        Assert.Equal(3, _handler.SentRequests.Count);
        Assert.Contains("ids=201&", _handler.SentRequests[2].RequestUri!.Query);
    }

    [Theory]
    [InlineData("tree")]
    [InlineData("oneHop")]
    public async Task GetWorkItemsByQueryAsync_LinkQuery_ThrowsInsteadOfReadingAsEmpty(string queryType)
    {
        _handler.EnqueueResponse(
            HttpStatusCode.OK,
            $$$"""{"queryType":"{{{queryType}}}","queryResultType":"workItemLink","workItemRelations":[{"target":{"id":1}}]}""");

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => _client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItemLinks"));

        Assert.Contains(queryType, ex.Message);
        Assert.Single(_handler.SentRequests);
    }

    [Fact]
    public async Task GetWorkItemsByQueryAsync_FlatQueryType_ReturnsItems()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"queryType":"flat","workItems":[{"id":1}]}""");
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":1,"value":[{"id":1,"fields":{}}]}""");

        var result = await _client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems");

        Assert.Equal([1], result.Select(w => w.Id));
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
