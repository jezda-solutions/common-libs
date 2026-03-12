using System.Text.Json.Serialization;

namespace Jezda.Common.Integrations.Trello.Models;

public sealed class TrelloBoard
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string? Desc { get; set; }

    [JsonPropertyName("closed")]
    public bool Closed { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public sealed class TrelloCard
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("closed")]
    public bool Closed { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("idBoard")]
    public string IdBoard { get; set; } = string.Empty;

    [JsonPropertyName("idList")]
    public string IdList { get; set; } = string.Empty;
}

public sealed class TrelloMember
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}
