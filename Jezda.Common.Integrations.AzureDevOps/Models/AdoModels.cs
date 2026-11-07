using System.Text.Json.Serialization;

namespace Jezda.Common.Integrations.AzureDevOps.Models;

public sealed class AdoWorkItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("fields")]
    public Dictionary<string, object> Fields { get; set; } = new();

    public string Title => Field("System.Title");

    public string State => Field("System.State");

    // The owning team project. Needed when the query was not scoped to one project — an
    // organisation-wide WIQL search returns work items from many, and the project cannot be
    // inferred from the request. `_apis/wit/workitems?ids=` returns this field by default.
    public string TeamProject => Field("System.TeamProject");

    // Azure DevOps returns work item fields as an untyped bag, and which keys are present depends
    // on the process template and on whether the request narrowed `fields`. Every read is
    // therefore "absent and null both mean empty".
    private string Field(string name) =>
        Fields.TryGetValue(name, out var value) ? value?.ToString() ?? string.Empty : string.Empty;
}

public sealed class AdoWiqlRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;
}

public sealed class AdoWiqlResponse
{
    [JsonPropertyName("queryType")]
    public string? QueryType { get; set; }

    [JsonPropertyName("workItems")]
    public List<AdoWorkItemReference> WorkItems { get; set; } = new();
}

public sealed class AdoWorkItemReference
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class AdoWorkItemListResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    /// <summary>
    /// The requested work items. With <c>errorPolicy=omit</c>, an item that was deleted or cannot be
    /// read is a <see langword="null"/> slot. The list itself is <see langword="null"/> when the
    /// answer had none, which is not the same as every item being omitted.
    /// </summary>
    [JsonPropertyName("value")]
    public List<AdoWorkItem?>? Value { get; set; }
}

public sealed class JsonPatchOperation
{
    [JsonPropertyName("op")]
    public string Op { get; set; } = "add";

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}

public sealed class AdoProjectListResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("value")]
    public List<AdoProject> Value { get; set; } = new();
}

public sealed class AdoProject
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("revision")]
    public long Revision { get; set; }

    [JsonPropertyName("visibility")]
    public string? Visibility { get; set; }

    [JsonPropertyName("lastUpdateTime")]
    public DateTimeOffset LastUpdateTime { get; set; }
}
