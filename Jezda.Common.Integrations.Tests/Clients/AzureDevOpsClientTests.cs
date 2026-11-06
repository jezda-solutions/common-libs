using System.Net;
using System.Text.Json;
using Jezda.Common.Integrations.AzureDevOps;
using Jezda.Common.Integrations.AzureDevOps.Configuration;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Clients;

public class AzureDevOpsClientTests
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset To = new(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);

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

    [Fact]
    public async Task GetCompletedWorkLogsAsync_SkipsItemsTheDetailsBatchOmitted()
    {
        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(7, 8));
        _handler.EnqueueResponse(
            HttpStatusCode.OK,
            """{"count":2,"value":[null,{"id":8,"fields":{"System.TeamProject":"Platform"}}]}""");
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":0,"value":[]}""");

        await _client.GetCompletedWorkLogsAsync(from, to);

        // The omitted id is dropped before the per-item updates calls, so only the survivor is asked for.
        Assert.Contains("errorPolicy=omit", _handler.SentRequests[1].RequestUri!.Query);
        Assert.Equal(3, _handler.SentRequests.Count);
        Assert.EndsWith("/_apis/wit/workitems/8/updates", _handler.SentRequests[2].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetCompletedWorkLogsAsync_PositiveDelta_ReturnsEntry()
    {
        EnqueueWorkItemWithUpdates("""
            {
              "id": 1,
              "rev": 2,
              "revisedDate": "2026-01-15T10:00:00Z",
              "revisedBy": { "uniqueName": "ana@example.com", "displayName": "Ana" },
              "fields": {
                "Microsoft.VSTS.Scheduling.CompletedWork": { "oldValue": 1.0, "newValue": 3.5 }
              }
            }
            """);

        var entries = await _client.GetCompletedWorkLogsAsync(From, To);

        var entry = Assert.Single(entries);
        Assert.Equal(42, entry.WorkItemId);
        Assert.Equal(2.5, entry.Hours);
        Assert.Equal("ana@example.com", entry.UserUniqueName);
        Assert.Equal("Ana", entry.UserDisplayName);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero), entry.LoggedAt);
        // System.TeamProject arrives as a JsonElement; ToString() must give the bare string.
        Assert.Equal("MyProject", entry.Project);
    }

    [Fact]
    public async Task GetCompletedWorkLogsAsync_MissingOldValue_TreatsOldValueAsZero()
    {
        // The first time CompletedWork is set, Azure DevOps sends only newValue.
        EnqueueWorkItemWithUpdates("""
            {
              "id": 1,
              "rev": 2,
              "revisedDate": "2026-01-15T10:00:00Z",
              "fields": {
                "Microsoft.VSTS.Scheduling.CompletedWork": { "newValue": 4 }
              }
            }
            """);

        var entries = await _client.GetCompletedWorkLogsAsync(From, To);

        var entry = Assert.Single(entries);
        Assert.Equal(4, entry.Hours);
    }

    [Fact]
    public async Task GetCompletedWorkLogsAsync_StringValues_AreParsedInvariantly()
    {
        EnqueueWorkItemWithUpdates("""
            {
              "id": 1,
              "rev": 2,
              "revisedDate": "2026-01-15T10:00:00Z",
              "fields": {
                "Microsoft.VSTS.Scheduling.CompletedWork": { "oldValue": "0.5", "newValue": "2.25" }
              }
            }
            """);

        var entries = await _client.GetCompletedWorkLogsAsync(From, To);

        var entry = Assert.Single(entries);
        Assert.Equal(1.75, entry.Hours);
    }

    [Fact]
    public async Task GetCompletedWorkLogsAsync_NegativeDelta_IsSkipped()
    {
        EnqueueWorkItemWithUpdates("""
            {
              "id": 1,
              "rev": 2,
              "revisedDate": "2026-01-15T10:00:00Z",
              "fields": {
                "Microsoft.VSTS.Scheduling.CompletedWork": { "oldValue": 5.0, "newValue": 3.0 }
              }
            }
            """);

        var entries = await _client.GetCompletedWorkLogsAsync(From, To);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetCompletedWorkLogsAsync_UpdateOutsideDateRange_IsSkipped()
    {
        EnqueueWorkItemWithUpdates("""
            {
              "id": 1,
              "rev": 2,
              "revisedDate": "2025-12-20T10:00:00Z",
              "fields": {
                "Microsoft.VSTS.Scheduling.CompletedWork": { "oldValue": 1.0, "newValue": 3.0 }
              }
            },
            {
              "id": 2,
              "rev": 3,
              "revisedDate": "2026-01-10T10:00:00Z",
              "fields": {
                "Microsoft.VSTS.Scheduling.CompletedWork": { "oldValue": 3.0, "newValue": 4.0 }
              }
            }
            """);

        var entries = await _client.GetCompletedWorkLogsAsync(From, To);

        var entry = Assert.Single(entries);
        Assert.Equal(1, entry.Hours);
        Assert.Equal(new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.Zero), entry.LoggedAt);
    }

    [Fact]
    public async Task GetCompletedWorkLogsAsync_LatestUpdateWithSentinelRevisedDate_IsIncludedAtItsChangedDate()
    {
        // The newest revision has not been superseded, so Azure DevOps gives it revisedDate 9999-01-01.
        EnqueueWorkItemWithUpdates("""
            {
              "id": 1,
              "rev": 2,
              "revisedDate": "9999-01-01T00:00:00Z",
              "fields": {
                "System.ChangedDate": { "oldValue": "2026-01-10T08:00:00Z", "newValue": "2026-01-15T10:00:00Z" },
                "Microsoft.VSTS.Scheduling.CompletedWork": { "oldValue": 1.0, "newValue": 3.0 }
              }
            }
            """);

        var entries = await _client.GetCompletedWorkLogsAsync(From, To);

        var entry = Assert.Single(entries);
        Assert.Equal(2, entry.Hours);
        Assert.Equal(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero), entry.LoggedAt);
    }

    [Fact]
    public async Task GetCompletedWorkLogsAsync_FiltersByChangedDate_NotByRevisedDate()
    {
        // revisedDate is when the next change superseded the revision, so it is later than the change.
        EnqueueWorkItemWithUpdates("""
            {
              "id": 1,
              "rev": 2,
              "revisedDate": "2026-01-05T09:00:00Z",
              "fields": {
                "System.ChangedDate": { "newValue": "2025-12-31T17:00:00Z" },
                "Microsoft.VSTS.Scheduling.CompletedWork": { "newValue": 2.0 }
              }
            },
            {
              "id": 2,
              "rev": 3,
              "revisedDate": "2026-02-03T09:00:00Z",
              "fields": {
                "System.ChangedDate": { "oldValue": "2026-01-05T09:00:00Z", "newValue": "2026-01-31T20:00:00Z" },
                "Microsoft.VSTS.Scheduling.CompletedWork": { "oldValue": 2.0, "newValue": 6.0 }
              }
            }
            """);

        var entries = await _client.GetCompletedWorkLogsAsync(From, To);

        // The December change is out even though its revisedDate is in January; the January 31
        // change is in even though its revisedDate is in February.
        var entry = Assert.Single(entries);
        Assert.Equal(4, entry.Hours);
        Assert.Equal(new DateTimeOffset(2026, 1, 31, 20, 0, 0, TimeSpan.Zero), entry.LoggedAt);
    }

    [Fact]
    public async Task GetCompletedWorkLogsAsync_SentinelRevisedDateWithoutChangedDate_IsSkipped()
    {
        EnqueueWorkItemWithUpdates("""
            {
              "id": 1,
              "rev": 2,
              "revisedDate": "9999-01-01T00:00:00Z",
              "fields": {
                "Microsoft.VSTS.Scheduling.CompletedWork": { "oldValue": 1.0, "newValue": 3.0 }
              }
            }
            """);

        var entries = await _client.GetCompletedWorkLogsAsync(From, To);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetCompletedWorkLogsAsync_QueryHasNoUpperBound()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs());

        await _client.GetCompletedWorkLogsAsync(From, To);

        // An item edited again after `to` can still hold work logged inside the range.
        var body = await _handler.SentRequests[0].Content!.ReadAsStringAsync();
        var query = JsonDocument.Parse(body).RootElement.GetProperty("query").GetString();
        Assert.Contains("[System.ChangedDate] >= '2026-01-01T00:00:00", query);
        Assert.DoesNotContain("<=", query);
    }

    // Queues the three calls GetCompletedWorkLogsAsync makes for a single work item (id 42):
    // the WIQL query, the work item details batch, then that item's updates.
    private void EnqueueWorkItemWithUpdates(string updatesJson)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, """
            { "queryType": "flat", "workItems": [ { "id": 42 } ] }
            """);
        _handler.EnqueueResponse(HttpStatusCode.OK, """
            {
              "count": 1,
              "value": [
                { "id": 42, "fields": { "System.TeamProject": "MyProject", "System.Title": "Task" } }
              ]
            }
            """);
        _handler.EnqueueResponse(HttpStatusCode.OK, $$"""
            { "count": 1, "value": [ {{updatesJson}} ] }
            """);
    }

    private static string Refs(params int[] ids) =>
        $$"""{"workItems":[{{string.Join(",", ids.Select(id => $$"""{"id":{{id}}}"""))}}]}""";
}
