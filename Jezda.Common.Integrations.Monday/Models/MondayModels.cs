using System.Text.Json.Serialization;

namespace Jezda.Common.Integrations.Monday.Models;

public sealed class MondayGraphQlRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// GraphQL variables, so values such as ids and cursors never become part of the query text.
    /// </summary>
    [JsonPropertyName("variables")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Variables { get; set; }
}

public sealed class MondayGraphQlResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("errors")]
    public List<MondayGraphQlError>? Errors { get; set; }
}

public sealed class MondayGraphQlError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("extensions")]
    public MondayGraphQlErrorExtensions? Extensions { get; set; }
}

public sealed class MondayGraphQlErrorExtensions
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    /// <summary>
    /// The HTTP status Monday.com assigns to an application error it sends with 200.
    /// </summary>
    [JsonPropertyName("status_code")]
    public int? StatusCode { get; set; }

    /// <summary>
    /// Seconds to wait before retrying, sent with rate limit errors.
    /// </summary>
    [JsonPropertyName("retry_in_seconds")]
    public double? RetryInSeconds { get; set; }
}

public sealed class MondayMeData
{
    [JsonPropertyName("me")]
    public MondayUser? Me { get; set; }
}

public sealed class MondayUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

public sealed class MondayBoardsData
{
    /// <summary>
    /// Null when the answer has no list at all, which is not the same as an empty one.
    /// </summary>
    [JsonPropertyName("boards")]
    public List<MondayBoard>? Boards { get; set; }
}

public sealed class MondayBoard
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class MondayBoardItemsData
{
    /// <summary>
    /// Null when the answer has no list at all, which is not the same as an empty one.
    /// </summary>
    [JsonPropertyName("boards")]
    public List<MondayBoardWithItems>? Boards { get; set; }
}

public sealed class MondayNextItemsPageData
{
    [JsonPropertyName("next_items_page")]
    public MondayItemsPage? NextItemsPage { get; set; }
}

public sealed class MondayBoardWithItems
{
    [JsonPropertyName("items_page")]
    public MondayItemsPage? ItemsPage { get; set; }
}

public sealed class MondayItemsPage
{
    [JsonPropertyName("items")]
    public List<MondayItem> Items { get; set; } = [];

    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

public sealed class MondayItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}
