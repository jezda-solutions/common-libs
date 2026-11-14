using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
using Jezda.Common.Integrations.Http;
using Jezda.Common.Integrations.Monday.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Jezda.Common.Integrations.Monday.Providers;

public sealed class MondayTaskProvider(
    IHttpClientFactory httpClientFactory,
    ILogger<MondayTaskProvider> logger) : IExternalTaskProvider
{
    public const string HttpClientName = "ExternalTaskProvider.Monday";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly HashSet<string?> RejectedTokenCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "NOT_AUTHENTICATED",
        "Unauthorized",
        "UserUnauthorizedException",
        "USER_ACCESS_DENIED",
        "missingRequiredPermissions"
    };

    private static readonly string[] RefusalsNotAboutTheToken = ["ip is restricted", "API_TEMPORARILY_BLOCKED"];

    public ExternalProvider Provider => ExternalProvider.Monday;

    public async Task<ConnectionValidationResult> ValidateConnectionAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        if (!AccessTokenRules.CanGoInHeader(accessToken))
        {
            logger.LogWarning("Monday.com token has characters that cannot go in a header, so it was not sent");
            return ConnectionValidationResult.Rejected(TokenRejectionReason.NotSendable);
        }

        using var client = CreateClient(accessToken);
        using var response = await client.PostAsJsonAsync(
            "", new MondayGraphQlRequest { Query = "{ me { id } }" }, JsonOptions, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var body = await response.Content.ReadAsUtf8StringAsync(cancellationToken);

            if (RefusalsNotAboutTheToken.Any(refusal => body.Contains(refusal, StringComparison.OrdinalIgnoreCase)))
            {
                throw new HttpRequestException(
                    "Monday.com refused the request for a reason other than the token: an IP restriction or a temporary API block.",
                    null,
                    response.StatusCode);
            }

            logger.LogWarning("Monday.com rejected the token with {StatusCode}", (int)response.StatusCode);
            return ConnectionValidationResult.Rejected(TokenRejectionReason.InvalidToken, "401 on the GraphQL endpoint");
        }

        // A 403 can come from a gateway in front of Monday.com, so only a known code blames the token.
        if (response.StatusCode == HttpStatusCode.Forbidden
            && RejectedTokenCode(await response.Content.ReadAsUtf8StringAsync(cancellationToken)) is { } forbiddenCode)
        {
            logger.LogWarning("Monday.com rejected the token with {StatusCode}: {Code}", (int)response.StatusCode, forbiddenCode);
            return ConnectionValidationResult.Rejected(ReasonFor(forbiddenCode), $"403 with {forbiddenCode}");
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromUtf8JsonAsync<MondayGraphQlResponse<JsonElement>>(JsonOptions, cancellationToken);

        if (ReadData<MondayMeData>(result)?.Me is not null)
        {
            return ConnectionValidationResult.Accepted;
        }

        // Monday reports application errors with status 200, so only the code tells them apart.
        var errors = result?.Errors ?? [];

        if (errors.Select(error => error.Extensions?.Code).FirstOrDefault(RejectedTokenCodes.Contains) is { } code)
        {
            logger.LogWarning("Monday.com rejected the token with a GraphQL error: {Code}", code);
            return ConnectionValidationResult.Rejected(ReasonFor(code), $"{(int)response.StatusCode} with {code}");
        }

        var reasons = DescribeErrors(errors);

        throw new HttpRequestException(
            $"Monday.com did not return the current user: {(reasons.Length > 0 ? reasons : "no error reported")}.",
            null,
            response.StatusCode);
    }

    private static string? RejectedTokenCode(string body)
    {
        try
        {
            var errors = JsonSerializer.Deserialize<MondayGraphQlResponse<JsonElement>>(body, JsonOptions)?.Errors;

            return errors?.Select(error => error.Extensions?.Code).FirstOrDefault(RejectedTokenCodes.Contains);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // A token Monday.com knows but that lacks a scope, or a user it denies, is not a token to replace.
    private static TokenRejectionReason ReasonFor(string code) => code switch
    {
        _ when code.Equals("missingRequiredPermissions", StringComparison.OrdinalIgnoreCase) => TokenRejectionReason.MissingScope,
        _ when code.Equals("USER_ACCESS_DENIED", StringComparison.OrdinalIgnoreCase) => TokenRejectionReason.AccessDenied,
        _ => TokenRejectionReason.InvalidToken
    };

    private const int BoardsPageSize = 100;

    // The most Monday.com allows per items_page and next_items_page.
    private const int ItemsPageSize = 500;

    private const string ItemFields = "cursor items { id name state }";

    public async Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        var boards = new List<MondayBoard>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (var page = 1; ; page++)
        {
            var data = await ExecuteGraphQlAsync<MondayBoardsData>(
                client,
                $"query ($page: Int!) {{ boards(limit: {BoardsPageSize}, page: $page) {{ id name description }} }}",
                new { page },
                cancellationToken);

            var pageBoards = data.Boards ?? throw new HttpRequestException(
                "Monday.com answered without a board list and without reporting an error.",
                null,
                HttpStatusCode.OK);

            // A page that repeats boards already read would page forever.
            if (pageBoards.Count > 0 && !seenIds.Add(pageBoards[0].Id))
            {
                throw new HttpRequestException(
                    $"Monday.com returned board {pageBoards[0].Id} again on page {page}; the board list would never end.",
                    null,
                    HttpStatusCode.OK);
            }

            seenIds.UnionWith(pageBoards.Select(b => b.Id));
            boards.AddRange(pageBoards);

            if (pageBoards.Count < BoardsPageSize)
            {
                break;
            }
        }

        return [.. boards.Select(b => new ExternalProjectDto
        {
            Id = b.Id,
            Path = b.Id, // A board id is Monday's only identifier for it, and it does not change.
            Name = b.Name,
            Description = b.Description,
            Url = $"https://monday.com/boards/{b.Id}",
            Provider = ExternalProvider.Monday
        })];
    }

    /// <summary>
    /// Lists all items of a board, following Monday.com's cursor to the last page.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="projectId"/> is not a board id (digits only).</exception>
    /// <exception cref="HttpRequestException">
    /// With <see cref="HttpStatusCode.NotFound"/> when Monday.com does not return the board: deleted,
    /// archived, never existed, or the token cannot see it. Never an empty list for that case, because a
    /// sync would read it as every task gone.
    /// </exception>
    public async Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(string accessToken, string projectId, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);

        if (!projectId.All(char.IsAsciiDigit))
        {
            throw new ArgumentException($"A Monday.com board id is a number, not '{projectId}'.", nameof(projectId));
        }

        using var client = CreateClient(accessToken);

        var data = await ExecuteGraphQlAsync<MondayBoardItemsData>(
            client,
            $"query ($board: [ID!]) {{ boards(ids: $board) {{ items_page(limit: {ItemsPageSize}) {{ {ItemFields} }} }} }}",
            new { board = new[] { projectId } },
            cancellationToken);

        if (data.Boards is not { Count: > 0 } boards)
        {
            throw new HttpRequestException(
                $"Monday.com did not return board {projectId}: it was deleted or archived, never existed, or the token cannot see it.",
                null,
                HttpStatusCode.NotFound);
        }

        var page = boards[0].ItemsPage ?? throw new HttpRequestException(
            $"Monday.com returned board {projectId} without its items and without reporting an error.",
            null,
            HttpStatusCode.OK);

        var items = new List<MondayItem>(page.Items ?? []);
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrEmpty(page.Cursor))
        {
            if (!seenCursors.Add(page.Cursor))
            {
                throw new HttpRequestException(
                    "Monday.com returned the same cursor twice; the item list would never end.",
                    null,
                    HttpStatusCode.OK);
            }

            var next = await ExecuteGraphQlAsync<MondayNextItemsPageData>(
                client,
                $"query ($cursor: String!) {{ next_items_page(limit: {ItemsPageSize}, cursor: $cursor) {{ {ItemFields} }} }}",
                new { cursor = page.Cursor },
                cancellationToken);

            page = next.NextItemsPage ?? throw new HttpRequestException(
                $"Monday.com answered the next page of board {projectId} without items and without reporting an error.",
                null,
                HttpStatusCode.OK);

            items.AddRange(page.Items ?? []);
        }

        return [.. items.Select(i => new ExternalTaskDto
        {
            Id = i.Id,
            Title = i.Name,
            Status = i.State,
            Url = $"https://monday.com/boards/{projectId}/pulses/{i.Id}",
            ProjectId = projectId,
            Provider = ExternalProvider.Monday
        })];
    }

    /// <summary>
    /// The fan-out search, except that a board Monday.com does not return or refuses (404 or 403) is
    /// skipped with a warning instead of failing the whole search. A rejected token on the board list,
    /// a rate limit or an outage still fails it.
    /// </summary>
    public async Task<IReadOnlyList<ExternalTaskDto>> SearchTasksAsync(
        string accessToken,
        string searchTerm,
        int limit = 20,
        string? baseUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || limit <= 0)
        {
            return [];
        }

        var term = searchTerm.Trim();
        var projects = await GetProjectsAsync(accessToken, baseUrl, cancellationToken);
        var matches = new List<ExternalTaskDto>();

        foreach (var project in projects)
        {
            if (matches.Count >= limit)
            {
                break;
            }

            IReadOnlyList<ExternalTaskDto> tasks;

            try
            {
                tasks = await GetTasksAsync(accessToken, project.Id, baseUrl, cancellationToken);
            }
            // The board list came back, so the token works: a 403 here is about this one board.
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
            {
                logger.LogWarning("Monday.com search skipped board {BoardId}: {Reason}", project.Id, ex.Message);
                continue;
            }

            matches.AddRange(tasks.Where(t =>
                t.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || t.Id.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        return matches.Count > limit ? matches[..limit] : matches;
    }

    private HttpClient CreateClient(string accessToken)
    {
        AccessTokenRules.ThrowIfCannotGoInHeader(accessToken);

        var client = httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static readonly HashSet<string?> RateLimitCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "COMPLEXITY_BUDGET_EXHAUSTED",
        "RATE_LIMIT_EXCEEDED",
        "Rate Limit Exceeded",
        "Minute limit rate exceeded",
        "maxConcurrencyExceeded",
        "IP_RATE_LIMIT_EXCEEDED",
        "DAILY_LIMIT_EXCEEDED",
        "API_TEMPORARILY_BLOCKED"
    };

    // Monday.com's answers when the board asked for is gone or out of reach.
    private static readonly HashSet<string?> BoardUnavailableCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "InvalidBoardIdException",
        "ResourceNotFoundException"
    };

    /// <summary>
    /// Sends a query and returns its data, or throws an exception that says what went wrong:
    /// <see cref="IntegrationRateLimitException"/> for a rate limit, budget or temporary block;
    /// <see cref="HttpRequestException"/> with 401 or 403 for a refused token, 404 for a board out
    /// of reach, and the response's status for anything else.
    /// </summary>
    private static async Task<T> ExecuteGraphQlAsync<T>(
        HttpClient client, string query, object? variables, CancellationToken cancellationToken)
        where T : class
    {
        var request = new MondayGraphQlRequest { Query = query, Variables = variables };
        using var response = await client.PostAsJsonAsync("", request, JsonOptions, cancellationToken);

        var result = await ReadGraphQlResponseAsync(response, cancellationToken);

        // Monday reports application errors with status 200, and can send them next to partial data.
        // Callers read these lists as complete, so a list that may have gaps is a failure too.
        if (result?.Errors is { Count: > 0 } errors)
        {
            throw ToException(response, errors);
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new IntegrationRateLimitException(
                "Monday.com answered 429 without saying which limit was reached.",
                RetryAt(response, []));
        }

        response.EnsureSuccessStatusCode();

        return ReadData<T>(result) ?? throw new HttpRequestException(
            "Monday.com answered without data and without reporting an error.",
            null,
            response.StatusCode);
    }

    /// <summary>
    /// Reads the body whatever the status, because Monday.com puts the error code there for 4xx too.
    /// A success that is not JSON came from something other than the API, such as a proxy.
    /// </summary>
    private static async Task<MondayGraphQlResponse<JsonElement>?> ReadGraphQlResponseAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsUtf8StringAsync(cancellationToken);

        try
        {
            return JsonSerializer.Deserialize<MondayGraphQlResponse<JsonElement>>(body, JsonOptions);
        }
        catch (JsonException)
        {
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "no content type";

            throw new HttpRequestException(
                $"Monday.com answered {(int)response.StatusCode} with {mediaType} that is not a GraphQL answer; a proxy or a login page likely answered in its place.",
                null,
                response.StatusCode);
        }
    }

    private static HttpRequestException ToException(HttpResponseMessage response, IReadOnlyList<MondayGraphQlError> errors)
    {
        var description = $"Monday.com answered with errors: {DescribeErrors(errors)}.";
        var codes = errors.Select(error => error.Extensions?.Code ?? error.Message).ToList();

        if (response.StatusCode == HttpStatusCode.TooManyRequests || codes.Any(RateLimitCodes.Contains))
        {
            return new IntegrationRateLimitException(description, RetryAt(response, errors));
        }

        if (codes.FirstOrDefault(RejectedTokenCodes.Contains) is { } rejected)
        {
            var status = errors.Select(error => error.Extensions?.StatusCode).FirstOrDefault(code => code is 401 or 403)
                         ?? (ReasonFor(rejected) == TokenRejectionReason.InvalidToken ? 401 : 403);

            return new HttpRequestException(description, null, (HttpStatusCode)status);
        }

        if (codes.Any(BoardUnavailableCodes.Contains))
        {
            return new HttpRequestException(description, null, HttpStatusCode.NotFound);
        }

        var reported = errors.Select(error => error.Extensions?.StatusCode).FirstOrDefault(code => code is >= 400 and < 600);

        return new HttpRequestException(description, null, reported is { } code ? (HttpStatusCode)code : response.StatusCode);
    }

    private static DateTimeOffset? RetryAt(HttpResponseMessage response, IEnumerable<MondayGraphQlError> errors)
    {
        var seconds = errors.Select(error => error.Extensions?.RetryInSeconds).Where(s => s is > 0).Max();

        if (seconds is { } fromBody)
        {
            return DateTimeOffset.UtcNow.AddSeconds(fromBody);
        }

        var retryAfter = response.Headers.RetryAfter;

        return retryAfter?.Delta is { } delta ? DateTimeOffset.UtcNow.Add(delta) : retryAfter?.Date;
    }

    // An error-only answer can carry "data": [], so the data is read only once it is known to be an object.
    private static T? ReadData<T>(MondayGraphQlResponse<JsonElement>? result)
        where T : class =>
        result?.Data.ValueKind == JsonValueKind.Object ? result.Data.Deserialize<T>(JsonOptions) : null;

    private static string DescribeErrors(IEnumerable<MondayGraphQlError> errors) =>
        string.Join(", ", errors.Select(error => error.Extensions?.Code ?? error.Message));
}
