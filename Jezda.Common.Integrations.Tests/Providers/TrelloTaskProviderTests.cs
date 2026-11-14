using System.Net;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Trello.Providers;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Providers;

public class TrelloTaskProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly TrelloTaskProvider _provider;

    public TrelloTaskProviderTests()
    {
        var factory = new MockHttpClientFactory(
            _handler,
            TrelloTaskProvider.HttpClientName,
            new Uri("https://api.trello.com/1/"));

        _provider = new TrelloTaskProvider(factory, NullLogger<TrelloTaskProvider>.Instance);
    }

    [Fact]
    public void Provider_ReturnsTrello()
    {
        Assert.Equal(ExternalProvider.Trello, _provider.Provider);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithValidToken_ReturnsTrue()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { id = "member1", username = "testuser" });

        var result = await _provider.ValidateConnectionAsync(
            TrelloTaskProvider.FormatAccessToken("mykey", "mytoken"));

        Assert.True(result.IsValid);
        Assert.Single(_handler.SentRequests);
        var request = _handler.SentRequests[0];
        Assert.Equal("OAuth", request.Headers.Authorization?.Scheme);
        Assert.Equal("oauth_consumer_key=\"mykey\", oauth_token=\"mytoken\"", request.Headers.Authorization?.Parameter);
        Assert.Equal(string.Empty, request.RequestUri!.Query);
    }

    [Fact]
    public async Task EveryCall_KeepsTheKeyAndTokenOutOfTheUrl()
    {
        var accessToken = TrelloTaskProvider.FormatAccessToken("mykey", "mytoken");
        _handler.EnqueueResponse(HttpStatusCode.OK, Array.Empty<object>());
        _handler.EnqueueResponse(HttpStatusCode.OK, Array.Empty<object>());

        await _provider.GetProjectsAsync(accessToken);
        await _provider.GetTasksAsync(accessToken, "board1");

        Assert.All(_handler.SentRequests, request =>
        {
            Assert.DoesNotContain("mykey", request.RequestUri!.ToString());
            Assert.DoesNotContain("mytoken", request.RequestUri!.ToString());
            Assert.Equal("OAuth", request.Headers.Authorization?.Scheme);
        });
    }

    [Theory]
    [InlineData("key:tok\"en")]
    [InlineData("key:tok\\en")]
    [InlineData("k\u201Cey:token")]
    [InlineData("key:tok\nen")]
    public async Task ValidateConnectionAsync_KeyOrTokenThatCannotGoInTheHeader_ReturnsNotSendable(string accessToken)
    {
        var result = await _provider.ValidateConnectionAsync(accessToken);

        Assert.False(result.IsValid);
        Assert.Equal(TokenRejectionReason.NotSendable, result.Reason);
        Assert.Empty(_handler.SentRequests);
    }

    [Fact]
    public async Task GetTasksAsync_KeyOrTokenThatCannotGoInTheHeader_ThrowsWithoutSending()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetTasksAsync("key:tok\"en", "board1"));

        Assert.Empty(_handler.SentRequests);
    }

    [Fact]
    public async Task GetTasksAsync_EscapesTheBoardIdInThePath()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Array.Empty<object>());

        await _provider.GetTasksAsync("key:token", "a/b?c");

        Assert.Equal("/1/boards/a%2Fb%3Fc/cards", _handler.SentRequests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithInvalidToken_ReturnsFalse()
    {
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        var result = await _provider.ValidateConnectionAsync("badkey:badtoken");

        Assert.False(result.IsValid);
        Assert.Equal(TokenRejectionReason.InvalidToken, result.Reason);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ValidateConnectionAsync_OtherFailureStatus_ThrowsInsteadOfBlamingTheToken(HttpStatusCode status)
    {
        _handler.EnqueueResponse(status);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _provider.ValidateConnectionAsync("key:token"));

        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task ValidateConnectionAsync_ConnectionFailure_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueException(new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<HttpRequestException>(() => _provider.ValidateConnectionAsync("key:token"));
    }

    [Fact]
    public async Task ValidateConnectionAsync_Timeout_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueException(new TaskCanceledException("The request timed out"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _provider.ValidateConnectionAsync("key:token"));
    }

    [Fact]
    public async Task ValidateConnectionAsync_MalformedAccessToken_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.ValidateConnectionAsync("no-separator"));
    }

    [Fact]
    public async Task GetProjectsAsync_ReturnsExternalProjectDtos()
    {
        var boards = new object[]
        {
            new { id = "board1", name = "Board One", desc = "Description 1", closed = false, url = "https://trello.com/b/board1" },
            new { id = "board2", name = "Board Two", desc = "", closed = false, url = "https://trello.com/b/board2" }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, boards);

        var result = await _provider.GetProjectsAsync("key:token");

        Assert.Equal(2, result.Count);
        Assert.Equal("board1", result[0].Id);
        Assert.Equal("board1", result[0].Path);
        Assert.Equal("Board One", result[0].Name);
        Assert.Equal("Description 1", result[0].Description);
        Assert.Equal("https://trello.com/b/board1", result[0].Url);
        Assert.Equal(ExternalProvider.Trello, result[0].Provider);
    }

    [Fact]
    public async Task GetTasksAsync_ReturnsExternalTaskDtos()
    {
        var cards = new[]
        {
            new { id = "card1", name = "Fix bug", closed = false, url = "https://trello.com/c/card1", idBoard = "board1", idList = "list1" },
            new { id = "card2", name = "Add feature", closed = true, url = "https://trello.com/c/card2", idBoard = "board1", idList = "list2" }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, cards);

        var result = await _provider.GetTasksAsync("key:token", "board1");

        Assert.Equal(2, result.Count);
        Assert.Equal("card1", result[0].Id);
        Assert.Equal("Fix bug", result[0].Title);
        Assert.Equal("open", result[0].Status);
        Assert.Equal("board1", result[0].ProjectId);
        Assert.Equal(ExternalProvider.Trello, result[0].Provider);
        Assert.Equal("closed", result[1].Status);
    }

    [Fact]
    public async Task GetProjectsAsync_MapsMetadata()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new object[]
        {
            new
            {
                id = "b1",
                name = "Closed org board",
                closed = true,
                url = "https://trello.com/b/b1",
                dateLastActivity = "2025-01-02T03:04:05.000Z",
                prefs = new { permissionLevel = "org" }
            },
            new { id = "b2", name = "Public board", closed = false, url = "u", prefs = new { permissionLevel = "public" } },
            new { id = "b3", name = "No prefs", closed = false, url = "u" }
        });

        var result = await _provider.GetProjectsAsync("key:token");

        Assert.True(result[0].IsArchived);
        Assert.True(result[0].IsPrivate);
        Assert.Equal(DateTimeOffset.Parse("2025-01-02T03:04:05Z"), result[0].UpdatedAt);
        Assert.Null(result[0].CreatedAt);

        Assert.False(result[1].IsArchived);
        Assert.False(result[1].IsPrivate);
        Assert.Null(result[1].UpdatedAt);

        Assert.Null(result[2].IsPrivate);
    }
}
