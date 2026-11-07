using System.Net;
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
        Assert.Equal(TokenRejectionReason.InvalidToken, result.Reason);
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
    [InlineData("NOT_AUTHENTICATED")]
    [InlineData("Unauthorized")]
    [InlineData("UserUnauthorizedException")]
    [InlineData("USER_ACCESS_DENIED")]
    [InlineData("missingRequiredPermissions")]
    public async Task ValidateConnectionAsync_RejectionOnStatus200_ReturnsFalse(string code)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, ErrorBody(code));

        var result = await _provider.ValidateConnectionAsync("token");

        Assert.False(result.IsValid);
        Assert.Equal(TokenRejectionReason.InvalidToken, result.Reason);
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

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetProjectsAsync("token"));

        Assert.Contains(reason, ex.Message);
    }

    [Theory]
    [InlineData("""{"data":null,"errors":[{"message":"Refused","extensions":{"code":"API_TEMPORARILY_BLOCKED"}}]}""", "API_TEMPORARILY_BLOCKED")]
    [InlineData(ErrorWithEmptyDataArray, "UserUnauthorizedException")]
    [InlineData("""{"errors":[{"message":"Budget exhausted"}]}""", "Budget exhausted")]
    public async Task GetTasksAsync_GraphQlError_ThrowsInsteadOfReturningEmpty(string body, string reason)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, body);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.GetTasksAsync("token", "123"));

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
}
