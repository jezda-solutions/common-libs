using System.Text.Json.Serialization;

namespace Jezda.Common.Integrations.ClickUp.Models;

public sealed class ClickUpTeamsResponse
{
    [JsonPropertyName("teams")]
    public List<ClickUpTeam> Teams { get; set; } = [];
}

public sealed class ClickUpTeam
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string? Color { get; set; }
}

public sealed class ClickUpTasksResponse
{
    [JsonPropertyName("tasks")]
    public List<ClickUpTask> Tasks { get; set; } = [];
}

public sealed class ClickUpTask
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public ClickUpStatus? Status { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("space")]
    public ClickUpSpaceRef? Space { get; set; }
}

public sealed class ClickUpStatus
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public sealed class ClickUpSpaceRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}
