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

        Assert.True(result);
        Assert.Single(_handler.SentRequests);
        Assert.Contains("key=mykey", _handler.SentRequests[0].RequestUri!.ToString());
        Assert.Contains("token=mytoken", _handler.SentRequests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithInvalidToken_ReturnsFalse()
    {
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        var result = await _provider.ValidateConnectionAsync("badkey:badtoken");

        Assert.False(result);
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
}
