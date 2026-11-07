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

    public async Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        var data = await ExecuteGraphQlAsync<MondayBoardsData>(
            client, "{ boards(limit:100) { id name description } }", cancellationToken);

        var boards = data.Boards ?? [];

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

    public async Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(string accessToken, string projectId, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        var query = $$"""{ boards(ids:{{projectId}}) { items_page { items { id name state } } } }""";
        var data = await ExecuteGraphQlAsync<MondayBoardItemsData>(
            client, query, cancellationToken);

        var items = data.Boards?.FirstOrDefault()?.ItemsPage?.Items ?? [];

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

    private HttpClient CreateClient(string accessToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static async Task<T> ExecuteGraphQlAsync<T>(
        HttpClient client, string query, CancellationToken cancellationToken)
        where T : class
    {
        var request = new MondayGraphQlRequest { Query = query };
        using var response = await client.PostAsJsonAsync("", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromUtf8JsonAsync<MondayGraphQlResponse<JsonElement>>(JsonOptions, cancellationToken);

        // Monday reports application errors with status 200, and can send them next to partial data.
        // Callers read these lists as complete, so a list that may have gaps is a failure too.
        if (result?.Errors is { Count: > 0 } errors)
        {
            throw new HttpRequestException(
                $"Monday.com answered with errors: {DescribeErrors(errors)}.",
                null,
                response.StatusCode);
        }

        return ReadData<T>(result) ?? throw new HttpRequestException(
            "Monday.com answered without data and without reporting an error.",
            null,
            response.StatusCode);
    }

    // An error-only answer can carry "data": [], so the data is read only once it is known to be an object.
    private static T? ReadData<T>(MondayGraphQlResponse<JsonElement>? result)
        where T : class =>
        result?.Data.ValueKind == JsonValueKind.Object ? result.Data.Deserialize<T>(JsonOptions) : null;

    private static string DescribeErrors(IEnumerable<MondayGraphQlError> errors) =>
        string.Join(", ", errors.Select(error => error.Extensions?.Code ?? error.Message));
}
