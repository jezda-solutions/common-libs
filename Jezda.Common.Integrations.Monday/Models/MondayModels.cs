using System.Text.Json.Serialization;

namespace Jezda.Common.Integrations.Monday.Models;

public sealed class MondayGraphQlRequest
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;
}

public sealed class MondayGraphQlResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }
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
    [JsonPropertyName("boards")]
    public List<MondayBoard> Boards { get; set; } = [];
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
    [JsonPropertyName("boards")]
    public List<MondayBoardWithItems> Boards { get; set; } = [];
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
