using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Jezda.Common.Integrations.Http;
using Jezda.Common.Integrations.Trello.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Jezda.Common.Integrations.Trello.Providers;

public sealed class TrelloTaskProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<TrelloTaskProvider> logger) : IExternalTaskProvider
{
    public const string HttpClientName = "ExternalTaskProvider.Trello";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExternalProvider Provider => ExternalProvider.Trello;

    /// <summary>
    /// Formats the access token for Trello from separate API key and token.
    /// The returned string should be passed as the <c>accessToken</c> parameter.
    /// </summary>
    public static string FormatAccessToken(string apiKey, string apiToken) => $"{apiKey}:{apiToken}";

    public async Task<ConnectionValidationResult> ValidateConnectionAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var (key, token) = ParseAccessToken(accessToken);

        using var response = await client.GetAsync($"members/me?key={key}&token={token}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            logger.LogWarning("Trello rejected the key or token with {StatusCode}", (int)response.StatusCode);
            return ConnectionValidationResult.Rejected(TokenRejectionReason.InvalidToken, "401 on members/me");
        }

        response.EnsureSuccessStatusCode();

        return ConnectionValidationResult.Accepted;
    }

    public async Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var (key, token) = ParseAccessToken(accessToken);

        var boards = await client.GetFromUtf8JsonAsync<List<TrelloBoard>>(
            $"members/me/boards?key={key}&token={token}", JsonOptions, cancellationToken)
            ?? [];

        return [.. boards.Select(b => new ExternalProjectDto
        {
            Id = b.Id,
            Path = b.Id, // The API addresses a board by this id, and it does not change.
            Name = b.Name,
            Description = b.Desc,
            Url = b.Url,
            Provider = ExternalProvider.Trello,
            UpdatedAt = b.DateLastActivity,
            IsArchived = b.Closed,
            IsPrivate = b.Prefs?.PermissionLevel is { } level
                ? !level.Equals("public", StringComparison.OrdinalIgnoreCase)
                : null
        })];
    }

    public async Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(string accessToken, string projectId, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient();
        var (key, token) = ParseAccessToken(accessToken);

        var cards = await client.GetFromUtf8JsonAsync<List<TrelloCard>>(
            $"boards/{projectId}/cards?key={key}&token={token}", JsonOptions, cancellationToken)
            ?? [];

        return [.. cards.Select(c => new ExternalTaskDto
        {
            Id = c.Id,
            Title = c.Name,
            Status = c.Closed ? "closed" : "open",
            Url = c.Url,
            ProjectId = projectId,
            Provider = ExternalProvider.Trello
        })];
    }

    private HttpClient CreateClient()
    {
        return httpClientFactory.CreateClient(HttpClientName);
    }

    private static (string Key, string Token) ParseAccessToken(string accessToken)
    {
        var parts = accessToken.Split(':', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException(
                "Access token must be in the format 'apiKey:apiToken'. Use TrelloTaskProvider.FormatAccessToken() to create it.",
                nameof(accessToken));
        }

        return (parts[0], parts[1]);
    }
}
