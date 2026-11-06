using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Abstractions.Models;
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

    public async Task<bool> ValidateConnectionAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);
        using var response = await client.PostAsJsonAsync(
            "", new MondayGraphQlRequest { Query = "{ me { id } }" }, JsonOptions, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (RefusalsNotAboutTheToken.Any(refusal => body.Contains(refusal, StringComparison.OrdinalIgnoreCase)))
            {
                throw new HttpRequestException(
                    "Monday.com refused the request for a reason other than the token: an IP restriction or a temporary API block.",
                    null,
                    response.StatusCode);
            }

            logger.LogWarning("Monday.com rejected the token with {StatusCode}", (int)response.StatusCode);
            return false;
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MondayGraphQlResponse<MondayMeData>>(JsonOptions, cancellationToken);

        if (result?.Data?.Me is not null)
        {
            return true;
        }

        // Monday reports application errors with status 200, so only the code tells them apart.
        var errors = result?.Errors ?? [];

        if (errors.Any(error => RejectedTokenCodes.Contains(error.Extensions?.Code)))
        {
            logger.LogWarning("Monday.com rejected the token with a GraphQL error");
            return false;
        }

        var reasons = string.Join(", ", errors.Select(error => error.Extensions?.Code ?? error.Message));

        throw new HttpRequestException(
            $"Monday.com did not return the current user: {(reasons.Length > 0 ? reasons : "no error reported")}.",
            null,
            response.StatusCode);
    }

    public async Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl = null, CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(accessToken);

        var response = await ExecuteGraphQlAsync<MondayBoardsData>(
            client, "{ boards(limit:100) { id name description } }", cancellationToken);

        var boards = response?.Data?.Boards ?? [];

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
        var response = await ExecuteGraphQlAsync<MondayBoardItemsData>(
            client, query, cancellationToken);

        var items = response?.Data?.Boards?.FirstOrDefault()?.ItemsPage?.Items ?? [];

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

    private static async Task<MondayGraphQlResponse<T>?> ExecuteGraphQlAsync<T>(
        HttpClient client, string query, CancellationToken cancellationToken)
    {
        var request = new MondayGraphQlRequest { Query = query };
        var response = await client.PostAsJsonAsync("", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MondayGraphQlResponse<T>>(JsonOptions, cancellationToken);
    }
}
