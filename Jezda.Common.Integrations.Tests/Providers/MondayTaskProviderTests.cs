using System.Net;
using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Monday.Providers;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Providers;

public class MondayTaskProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly MondayTaskProvider _provider;

    public MondayTaskProviderTests()
    {
        var factory = new MockHttpClientFactory(
            _handler,
            MondayTaskProvider.HttpClientName,
            new Uri("https://api.monday.com/v2"));

        _provider = new MondayTaskProvider(factory, NullLogger<MondayTaskProvider>.Instance);
    }

    [Fact]
    public void Provider_ReturnsMonday()
    {
        Assert.Equal(ExternalProvider.Monday, _provider.Provider);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithValidToken_ReturnsTrue()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { data = new { me = new { id = "user1" } } });

        var result = await _provider.ValidateConnectionAsync("monday-api-token");

        Assert.True(result.IsValid);
        Assert.Single(_handler.SentRequests);
        Assert.Equal("Bearer", _handler.SentRequests[0].Headers.Authorization?.Scheme);
        Assert.Equal("monday-api-token", _handler.SentRequests[0].Headers.Authorization?.Parameter);
    }

    [Theory]
    [InlineData("token\u201D")] // A pasted smart quote
    [InlineData("tok\u00E9n")]
    [InlineData("token\n")] // A pasted line break
    [InlineData("token\r\n")]
    public async Task ValidateConnectionAsync_TokenThatCannotGoInAHeader_ReturnsFalseWithoutSending(string token)
    {
        var result = await _provider.ValidateConnectionAsync(token);

        Assert.False(result.IsValid);
        Assert.Equal(TokenRejectionReason.NotSendable, result.Reason);
        Assert.Empty(_handler.SentRequests);
    }

    [Fact]
    public async Task OtherCalls_TokenThatCannotGoInAHeader_ThrowWithoutSending()
    {
        // Sent, it would fail in the HTTP stack as an outage, be retried and count towards the
        // circuit breaker every caller of this provider shares.
        const string token = "token\u201D";

        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetProjectsAsync(token));
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetTasksAsync(token, "123"));
        await Assert.ThrowsAsync<ArgumentException>(() => ((IExternalTaskProvider)_provider).SearchTasksAsync(token, "bug"));
        Assert.Empty(_handler.SentRequests);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithInvalidToken_ReturnsFalse()
    {
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        var result = await _provider.ValidateConnectionAsync("bad-token");

        Assert.False(result.IsValid);
        Assert.Equal(TokenRejectionReason.InvalidToken, result.Reason);
    }

    [Fact]
    public async Task ValidateConnectionAsync_NotAuthenticatedBody_ReturnsFalse()
    {
        // What the live API answers a made-up token with.
        _handler.EnqueueResponse(
            HttpStatusCode.Unauthorized,
            """{"errors":[{"message":"Not authenticated","extensions":{"code":"NOT_AUTHENTICATED"}}]}""");

        var result = await _provider.ValidateConnectionAsync("bad-token");

        Assert.False(result.IsValid);
        Assert.Equal(TokenRejectionReason.InvalidToken, result.Reason);
    }

    [Fact]
    public async Task ValidateConnectionAsync_ForbiddenWithRejectionCode_ReturnsFalse()
    {
        _handler.EnqueueResponse(
            HttpStatusCode.Forbidden,
            """{"errors":[{"message":"Denied","extensions":{"code":"USER_ACCESS_DENIED"}}]}""");

        var result = await _provider.ValidateConnectionAsync("token");

        Assert.False(result.IsValid);
        Assert.Equal(TokenRejectionReason.AccessDenied, result.Reason);
        Assert.Equal("403 with USER_ACCESS_DENIED", result.Detail);
    }

    [Theory]
    [InlineData("<html>Blocked by gateway</html>")]
    [InlineData("""{"errors":[{"message":"Your ip is restricted"}]}""")]
    [InlineData("""{"errors":["Forbidden"]}""")]
    public async Task ValidateConnectionAsync_ForbiddenWithoutRejectionCode_ThrowsInsteadOfBlamingTheToken(string body)
    {
        _handler.EnqueueResponse(HttpStatusCode.Forbidden, body);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.ValidateConnectionAsync("token"));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Theory]
    [InlineData("NOT_AUTHENTICATED", TokenRejectionReason.InvalidToken)]
    [InlineData("Unauthorized", TokenRejectionReason.InvalidToken)]
    [InlineData("UserUnauthorizedException", TokenRejectionReason.InvalidToken)]
    [InlineData("USER_ACCESS_DENIED", TokenRejectionReason.AccessDenied)]
    [InlineData("missingRequiredPermissions", TokenRejectionReason.MissingScope)]
    public async Task ValidateConnectionAsync_RejectionOnStatus200_ReturnsReasonForTheCode(string code, TokenRejectionReason reason)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, ErrorBody(code));

        var result = await _provider.ValidateConnectionAsync("token");

        Assert.False(result.IsValid);
        Assert.Equal(reason, result.Reason);
        Assert.Equal($"200 with {code}", result.Detail);
    }

    [Fact]
    public async Task ValidateConnectionAsync_IpRestriction_ThrowsInsteadOfBlamingTheToken()
    {
        // An admin limited API access by address: the token is fine, the caller's address is not.
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized, """{"errors":[{"message":"Your ip is restricted"}]}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.ValidateConnectionAsync("token"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task ValidateConnectionAsync_ApiBlockedOnStatus200_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, ErrorBody("API_TEMPORARILY_BLOCKED"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.ValidateConnectionAsync("token"));

        Assert.Contains("API_TEMPORARILY_BLOCKED", ex.Message);
    }

    [Fact]
    public async Task ValidateConnectionAsync_NoUserAndNoErrors_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"data":{"me":null}}""");

        await Assert.ThrowsAsync<HttpRequestException>(() => _provider.ValidateConnectionAsync("token"));
    }

    [Fact]
    public async Task ValidateConnectionAsync_UserAlongsideErrors_ReturnsTrue()
    {
        // GraphQL can answer partially, and the user being there proves the token works.
        _handler.EnqueueResponse(
            HttpStatusCode.OK,
            """{"data":{"me":{"id":"1"}},"errors":[{"message":"Other","extensions":{"code":"USER_ACCESS_DENIED"}}]}""");

        var result = await _provider.ValidateConnectionAsync("token");

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ValidateConnectionAsync_OtherFailureStatus_ThrowsInsteadOfBlamingTheToken(HttpStatusCode status)
    {
        _handler.EnqueueResponse(status);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.ValidateConnectionAsync("token"));

        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task ValidateConnectionAsync_ConnectionFailure_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueException(new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<HttpRequestException>(() => _provider.ValidateConnectionAsync("token"));
    }

    [Fact]
    public async Task ValidateConnectionAsync_Timeout_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueException(new TaskCanceledException("The request timed out"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _provider.ValidateConnectionAsync("token"));
    }

    private static string ErrorBody(string code) =>
        $$$"""{"data":null,"errors":[{"message":"Refused","extensions":{"code":"{{{code}}}"}}]}""";

    [Fact]
    public async Task GetProjectsAsync_ReturnsExternalProjectDtos()
    {
        var response = new
        {
            data = new
            {
                boards = new[]
                {
                    new { id = "123", name = "Sprint Board", description = "Sprint planning" },
                    new { id = "456", name = "Backlog", description = "" }
                }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, response);

        var result = await _provider.GetProjectsAsync("monday-api-token");

        Assert.Equal(2, result.Count);
        Assert.Equal("123", result[0].Id);
        Assert.Equal("123", result[0].Path);
        Assert.Equal("Sprint Board", result[0].Name);
        Assert.Equal("Sprint planning", result[0].Description);
        Assert.Equal(ExternalProvider.Monday, result[0].Provider);
    }

    [Fact]
    public async Task GetTasksAsync_ReturnsExternalTaskDtos()
    {
        var response = new
        {
            data = new
            {
                boards = new[]
                {
                    new
                    {
                        items_page = new
                        {
                            items = new[]
                            {
                                new { id = "item1", name = "Design mockup", state = "active" },
                                new { id = "item2", name = "Write tests", state = "active" }
                            },
                            cursor = (string?)null
                        }
                    }
                }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, response);

        var result = await _provider.GetTasksAsync("monday-api-token", "123");

        Assert.Equal(2, result.Count);
        Assert.Equal("item1", result[0].Id);
        Assert.Equal("Design mockup", result[0].Title);
        Assert.Equal("active", result[0].Status);
        Assert.Equal("123", result[0].ProjectId);
        Assert.Equal(ExternalProvider.Monday, result[0].Provider);
    }

    // Monday's documented error-only answer carries "data": [], an array where an object is expected.
    private const string ErrorWithEmptyDataArray =
        """{"errors":[{"message":"Refused","extensions":{"code":"UserUnauthorizedException"}}],"data":[]}""";

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ValidateConnectionAsync_RejectionWithEmptyDataArray_ReturnsFalse(HttpStatusCode status)
    {
        _handler.EnqueueResponse(status, ErrorWithEmptyDataArray);

        var result = await _provider.ValidateConnectionAsync("token");

        Assert.False(result.IsValid);
        Assert.Equal(TokenRejectionReason.InvalidToken, result.Reason);
    }

    [Theory]
    [InlineData("""{"data":null,"errors":[{"message":"Refused","extensions":{"code":"API_TEMPORARILY_BLOCKED"}}]}""", "API_TEMPORARILY_BLOCKED")]
    [InlineData(ErrorWithEmptyDataArray, "UserUnauthorizedException")]
    [InlineData("""{"errors":[{"message":"Budget exhausted"}]}""", "Budget exhausted")]
    public async Task GetProjectsAsync_GraphQlError_ThrowsInsteadOfReturningEmpty(string body, string reason)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, body);

        var ex = await Assert.ThrowsAnyAsync<HttpRequestException>(() => _provider.GetProjectsAsync("token"));

        Assert.Contains(reason, ex.Message);
    }

    [Theory]
    [InlineData("""{"data":null,"errors":[{"message":"Refused","extensions":{"code":"API_TEMPORARILY_BLOCKED"}}]}""", "API_TEMPORARILY_BLOCKED")]
    [InlineData(ErrorWithEmptyDataArray, "UserUnauthorizedException")]
    [InlineData("""{"errors":[{"message":"Budget exhausted"}]}""", "Budget exhausted")]
    public async Task GetTasksAsync_GraphQlError_ThrowsInsteadOfReturningEmpty(string body, string reason)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, body);

        var ex = await Assert.ThrowsAnyAsync<HttpRequestException>(() => _provider.GetTasksAsync("token", "123"));

        Assert.Contains(reason, ex.Message);
    }

    [Fact]
    public async Task GetProjectsAsync_PartialDataWithErrors_ThrowsBecauseTheListMayBeIncomplete()
    {
        _handler.EnqueueResponse(
            HttpStatusCode.OK,
            """{"data":{"boards":[{"id":"1","name":"Kept"}]},"errors":[{"message":"Refused","extensions":{"code":"ASSET_UNAVAILABLE"}}]}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetProjectsAsync("token"));

        Assert.Contains("ASSET_UNAVAILABLE", ex.Message);
    }

    [Fact]
    public async Task GetTasksAsync_NulledFieldWithErrors_ThrowsBecauseTheListIsMissing()
    {
        _handler.EnqueueResponse(
            HttpStatusCode.OK,
            """{"data":{"boards":[{"items_page":null}]},"errors":[{"message":"Refused","extensions":{"code":"ASSET_UNAVAILABLE"}}]}""");

        await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetTasksAsync("token", "123"));
    }

    [Theory]
    [InlineData("""{"data":null}""")]
    [InlineData("""{"data":[]}""")]
    [InlineData("{}")]
    [InlineData("null")]
    public async Task GetProjectsAsync_NoDataAndNoErrors_ThrowsInsteadOfReturningEmpty(string body)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, body);

        await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetProjectsAsync("token"));
    }

    [Fact]
    public async Task GetProjectsAsync_NoBoards_ReturnsEmpty()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"data":{"boards":[]}}""");

        var result = await _provider.GetProjectsAsync("token");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTasksAsync_NoItems_ReturnsEmpty()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"data":{"boards":[{"items_page":{"items":[]}}]}}""");

        var result = await _provider.GetTasksAsync("token", "123");

        Assert.Empty(result);
    }
    private async Task<System.Text.Json.JsonElement> SentBody(int index)
    {
        var json = await _handler.SentRequests[index].Content!.ReadAsStringAsync();
        return System.Text.Json.JsonDocument.Parse(json).RootElement;
    }

    private static string BoardsPage(int from, int count) =>
        "{\"data\":{\"boards\":[" + string.Join(",", Enumerable.Range(from, count).Select(i => $"{{\"id\":\"{i}\",\"name\":\"B{i}\"}}")) + "]}}";

    [Fact]
    public async Task GetProjectsAsync_FullPage_ReadsTheNextOne()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, BoardsPage(1, 100));
        _handler.EnqueueResponse(HttpStatusCode.OK, BoardsPage(101, 3));

        var result = await _provider.GetProjectsAsync("token");

        Assert.Equal(103, result.Count);
        Assert.Equal(2, _handler.SentRequests.Count);
        Assert.Equal(1, (await SentBody(0)).GetProperty("variables").GetProperty("page").GetInt32());
        Assert.Equal(2, (await SentBody(1)).GetProperty("variables").GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task GetProjectsAsync_PageRepeatsBoards_ThrowsInsteadOfLooping()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, BoardsPage(1, 100));
        _handler.EnqueueResponse(HttpStatusCode.OK, BoardsPage(1, 100));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetProjectsAsync("token"));

        Assert.Contains("again", ex.Message);
        Assert.Equal(2, _handler.SentRequests.Count);
    }

    [Theory]
    [InlineData("""{"data":{}}""")]
    [InlineData("""{"data":{"boards":null}}""")]
    public async Task GetProjectsAsync_NoBoardList_ThrowsInsteadOfReturningEmpty(string body)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, body);

        await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetProjectsAsync("token"));
    }

    [Fact]
    public async Task GetTasksAsync_FollowsTheCursorToTheLastPage()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK,
            """{"data":{"boards":[{"items_page":{"cursor":"c1","items":[{"id":"1","name":"One","state":"active"}]}}]}}""");
        _handler.EnqueueResponse(HttpStatusCode.OK,
            """{"data":{"next_items_page":{"cursor":"c2","items":[{"id":"2","name":"Two","state":"active"}]}}}""");
        _handler.EnqueueResponse(HttpStatusCode.OK,
            """{"data":{"next_items_page":{"cursor":null,"items":[{"id":"3","name":"Three","state":"active"}]}}}""");

        var result = await _provider.GetTasksAsync("token", "123");

        Assert.Equal(["1", "2", "3"], result.Select(t => t.Id));
        Assert.All(result, t => Assert.Equal("123", t.ProjectId));

        var first = await SentBody(0);
        Assert.Equal("123", first.GetProperty("variables").GetProperty("board")[0].GetString());
        Assert.DoesNotContain("123", first.GetProperty("query").GetString());

        var second = await SentBody(1);
        Assert.Contains("next_items_page", second.GetProperty("query").GetString());
        Assert.Equal("c1", second.GetProperty("variables").GetProperty("cursor").GetString());
        Assert.Equal("c2", (await SentBody(2)).GetProperty("variables").GetProperty("cursor").GetString());
    }

    [Fact]
    public async Task GetTasksAsync_SameCursorTwice_ThrowsInsteadOfLooping()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK,
            """{"data":{"boards":[{"items_page":{"cursor":"same","items":[]}}]}}""");
        _handler.EnqueueResponse(HttpStatusCode.OK,
            """{"data":{"next_items_page":{"cursor":"same","items":[]}}}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetTasksAsync("token", "123"));

        Assert.Contains("same cursor", ex.Message);
    }

    [Theory]
    [InlineData("""{"data":{"boards":[]}}""")]
    [InlineData("""{"data":{"boards":null}}""")]
    [InlineData("""{"data":{}}""")]
    public async Task GetTasksAsync_BoardNotReturned_ThrowsNotFoundInsteadOfReturningEmpty(string body)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, body);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetTasksAsync("token", "123"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("123) { id } me(")]
    [InlineData("12 3")]
    public async Task GetTasksAsync_NotABoardId_ThrowsWithoutSending(string projectId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetTasksAsync("token", projectId));

        Assert.Empty(_handler.SentRequests);
    }

    [Theory]
    [InlineData("COMPLEXITY_BUDGET_EXHAUSTED")]
    [InlineData("maxConcurrencyExceeded")]
    [InlineData("API_TEMPORARILY_BLOCKED")]
    [InlineData("DAILY_LIMIT_EXCEEDED")]
    public async Task GetTasksAsync_RateLimitCode_ThrowsRateLimitWithRetryAt(string code)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK,
            $$$"""{"errors":[{"message":"Limit","extensions":{"code":"{{{code}}}","retry_in_seconds":30}}]}""");

        var before = DateTimeOffset.UtcNow;
        var ex = await Assert.ThrowsAsync<IntegrationRateLimitException>(() => _provider.GetTasksAsync("token", "123"));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Contains(code, ex.Message);
        Assert.InRange(ex.RetryAt!.Value, before.AddSeconds(29), DateTimeOffset.UtcNow.AddSeconds(31));
    }

    [Fact]
    public async Task GetProjectsAsync_Status429WithRetryAfter_ThrowsRateLimitWithThatDelay()
    {
        _handler.EnqueueResponse(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("""{"errors":[{"message":"Rate Limit Exceeded"}]}""", System.Text.Encoding.UTF8, "application/json")
            };
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(20));
            return response;
        });

        var before = DateTimeOffset.UtcNow;
        var ex = await Assert.ThrowsAsync<IntegrationRateLimitException>(() => _provider.GetProjectsAsync("token"));

        Assert.Contains("Rate Limit Exceeded", ex.Message);
        Assert.InRange(ex.RetryAt!.Value, before.AddSeconds(19), DateTimeOffset.UtcNow.AddSeconds(21));
    }

    [Fact]
    public async Task GetProjectsAsync_Status429WithoutBody_ThrowsRateLimit()
    {
        _handler.EnqueueResponse(HttpStatusCode.TooManyRequests, "not json");

        await Assert.ThrowsAsync<IntegrationRateLimitException>(() => _provider.GetProjectsAsync("token"));
    }

    [Theory]
    [InlineData("""{"errors":[{"message":"No","extensions":{"code":"UserUnauthorizedException","status_code":403}}]}""", HttpStatusCode.Forbidden)]
    [InlineData("""{"errors":[{"message":"No","extensions":{"code":"USER_ACCESS_DENIED"}}]}""", HttpStatusCode.Forbidden)]
    [InlineData("""{"errors":[{"message":"Unauthorized","extensions":{"code":"Unauthorized"}}]}""", HttpStatusCode.Unauthorized)]
    [InlineData("""{"errors":[{"message":"No","extensions":{"code":"InvalidBoardIdException"}}]}""", HttpStatusCode.NotFound)]
    [InlineData("""{"errors":[{"message":"No","extensions":{"code":"ResourceNotFoundException"}}]}""", HttpStatusCode.NotFound)]
    [InlineData("""{"errors":[{"message":"No","extensions":{"code":"RecordInvalidException","status_code":422}}]}""", HttpStatusCode.UnprocessableEntity)]
    [InlineData("""{"errors":[{"message":"Parse error on x"}]}""", HttpStatusCode.OK)]
    public async Task GetTasksAsync_GraphQlError_CarriesAStatusTheCallerCanActOn(string body, HttpStatusCode expected)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, body);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetTasksAsync("token", "123"));

        Assert.Equal(expected, ex.StatusCode);
    }

    [Fact]
    public async Task GetTasksAsync_ErrorOnUnauthorizedStatus_KeepsTheCode()
    {
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized,
            """{"errors":[{"message":"Unauthorized","extensions":{"code":"Unauthorized"}}]}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetTasksAsync("token", "123"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Contains("Unauthorized", ex.Message);
    }

    [Fact]
    public async Task GetProjectsAsync_HtmlOnSuccess_ThrowsHttpRequestException()
    {
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>login</html>", System.Text.Encoding.UTF8, "text/html")
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetProjectsAsync("token"));

        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
        Assert.Contains("not a GraphQL answer", ex.Message);
    }

    [Fact]
    public async Task GetProjectsAsync_ServerErrorWithoutJson_ThrowsWithThatStatus()
    {
        _handler.EnqueueResponse(HttpStatusCode.BadGateway, "<html>bad gateway</html>");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetProjectsAsync("token"));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
    }

    [Fact]
    public async Task SearchTasksAsync_SkipsABoardOutOfReachAndKeepsTheOthers()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"data":{"boards":[{"id":"1","name":"A"},{"id":"2","name":"B"},{"id":"3","name":"C"}]}}""");
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"data":{"boards":[{"items_page":{"items":[{"id":"11","name":"fix login","state":"active"}]}}]}}""");
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"data":{"boards":[]}}""");
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"errors":[{"message":"No","extensions":{"code":"UserUnauthorizedException","status_code":403}}]}""");

        var result = await ((IExternalTaskProvider)_provider).SearchTasksAsync("token", "login");

        Assert.Equal("11", Assert.Single(result).Id);
        Assert.Equal(4, _handler.SentRequests.Count);
    }

    [Fact]
    public async Task SearchTasksAsync_RateLimitOnABoard_FailsTheSearch()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"data":{"boards":[{"id":"1","name":"A"},{"id":"2","name":"B"}]}}""");
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"errors":[{"message":"Limit","extensions":{"code":"COMPLEXITY_BUDGET_EXHAUSTED"}}]}""");

        await Assert.ThrowsAsync<IntegrationRateLimitException>(() =>
            ((IExternalTaskProvider)_provider).SearchTasksAsync("token", "login"));
    }

    [Fact]
    public async Task SearchTasksAsync_StopsOnceTheLimitIsReached()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"data":{"boards":[{"id":"1","name":"A"},{"id":"2","name":"B"}]}}""");
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"data":{"boards":[{"items_page":{"items":[{"id":"11","name":"login a","state":"active"},{"id":"12","name":"login b","state":"active"}]}}]}}""");

        var result = await ((IExternalTaskProvider)_provider).SearchTasksAsync("token", "login", limit: 1);

        Assert.Single(result);
        Assert.Equal(2, _handler.SentRequests.Count);
    }
}
