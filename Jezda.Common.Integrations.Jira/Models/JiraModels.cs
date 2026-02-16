using System.Text.Json.Serialization;

namespace Jezda.Common.Integrations.Jira.Models;

public class JiraProject
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("projectTypeKey")]
    public string? ProjectTypeKey { get; set; }
}

public class JiraIssue
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public JiraIssueFields? Fields { get; set; }
}

public class JiraIssueFields
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public JiraStatus? Status { get; set; }

    [JsonPropertyName("project")]
    public JiraProject? Project { get; set; }

    [JsonPropertyName("priority")]
    public JiraPriority? Priority { get; set; }
}

public class JiraStatus
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class JiraPriority
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class JiraSearchResponse
{
    [JsonPropertyName("startAt")]
    public int StartAt { get; set; }

    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("issues")]
    public List<JiraIssue> Issues { get; set; } = new();
}
