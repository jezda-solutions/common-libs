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

    // Helper property to safely get title
    public string Title => Fields.TryGetValue("System.Title", out var title) ? title?.ToString() ?? string.Empty : string.Empty;

    // Helper property to safely get state
    public string State => Fields.TryGetValue("System.State", out var state) ? state?.ToString() ?? string.Empty : string.Empty;
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

    [JsonPropertyName("value")]
    public List<AdoWorkItem> Value { get; set; } = new();
}

public sealed class AdoWorkItemUpdatesResponse
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("value")]
    public List<AdoWorkItemUpdate> Value { get; set; } = new();
}

public sealed class AdoWorkItemUpdate
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("rev")]
    public int Rev { get; set; }

    [JsonPropertyName("revisedDate")]
    public DateTimeOffset RevisedDate { get; set; }

    [JsonPropertyName("revisedBy")]
    public AdoIdentity? RevisedBy { get; set; }

    [JsonPropertyName("fields")]
    public Dictionary<string, AdoFieldChange> Fields { get; set; } = new();
}

public sealed class AdoFieldChange
{
    [JsonPropertyName("oldValue")]
    public object? OldValue { get; set; }

    [JsonPropertyName("newValue")]
    public object? NewValue { get; set; }
}

public sealed class AdoIdentity
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("uniqueName")]
    public string? UniqueName { get; set; }
}

public sealed class AdoWorkLogEntry
{
    public int WorkItemId { get; set; }

    public string? Project { get; set; }

    public string? UserUniqueName { get; set; }

    public string? UserDisplayName { get; set; }

    public double Hours { get; set; }

    public DateTimeOffset LoggedAt { get; set; }
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
