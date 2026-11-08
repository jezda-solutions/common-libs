using System.Text.Json.Serialization;

namespace Jezda.Common.Integrations.Jira.Models;

public sealed class JiraProject
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("projectTypeKey")]
    public string? ProjectTypeKey { get; set; }

    [JsonPropertyName("archived")]
    public bool? Archived { get; set; }
}

public sealed class JiraIssue
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public JiraIssueFields? Fields { get; set; }
}

public sealed class JiraIssueFields
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

public sealed class JiraStatus
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class JiraPriority
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class JiraSearchRequest
{
    [JsonPropertyName("jql")]
    public string Jql { get; set; } = string.Empty;

    /// <summary>
    /// The <see cref="JiraSearchResponse.NextPageToken"/> of the previous page; null asks for the first.
    /// </summary>
    [JsonPropertyName("nextPageToken")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NextPageToken { get; set; }

    /// <summary>
    /// Page size. Jira caps it (100 when issue fields are requested) and may return fewer.
    /// </summary>
    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; } = 50;

    [JsonPropertyName("fields")]
    public string[] Fields { get; set; } = [];
}

public sealed class JiraSearchResponse
{
    /// <summary>
    /// Token for the next page; null on the last one. <c>search/jql</c> has no offset and no total.
    /// </summary>
    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }

    [JsonPropertyName("isLast")]
    public bool? IsLast { get; set; }

    [JsonPropertyName("issues")]
    public List<JiraIssue> Issues { get; set; } = new();
}
