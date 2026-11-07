using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Jezda.Common.Integrations.AzureDevOps;
using Jezda.Common.Integrations.AzureDevOps.Configuration;
using Jezda.Common.Integrations.AzureDevOps.Providers;
using Jezda.Common.Integrations.ClickUp.Providers;
using Jezda.Common.Integrations.GitHub.Providers;
using Jezda.Common.Integrations.Http;
using Jezda.Common.Integrations.Monday.Providers;
using Jezda.Common.Integrations.Tests.Helpers;
using Jezda.Common.Integrations.Trello.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Clients;

/// <summary>
/// Bodies declared in a charset .NET will not decode: it knows utf-7 by name but has it disabled
/// (<see cref="NotSupportedException"/>), and it cannot decode windows-1252 without the CodePages
/// provider (<see cref="InvalidOperationException"/>). Neither may replace the status, and JSON is
/// read as UTF-8 whatever the header says.
/// </summary>
public class UndecodableCharsetTests
{
    private readonly MockHttpMessageHandler _handler = new();

    [Theory]
    [InlineData("utf-7")]
    [InlineData("windows-1252")]
    public async Task ReadAsUtf8StringAsync_IgnoresTheCharset(string charset)
    {
        using var content = Body("""{"message":"blocked"}""", "text/plain", charset);

        Assert.Equal("""{"message":"blocked"}""", await content.ReadAsUtf8StringAsync());
    }

    [Fact]
    public async Task ReadAsUtf8StringAsync_InvalidUtf8_ReplacesInsteadOfThrowing()
    {
        using var content = new ByteArrayContent([0x61, 0xFF, 0x62]);

        Assert.Equal("a�b", await content.ReadAsUtf8StringAsync());
    }

    [Fact]
    public async Task ReadAsUtf8StringAsync_DropsAByteOrderMark()
    {
        using var content = new ByteArrayContent([0xEF, 0xBB, 0xBF, (byte)'o', (byte)'k']);

        Assert.Equal("ok", await content.ReadAsUtf8StringAsync());
    }

    [Theory]
    [InlineData("utf-7")]
    [InlineData("windows-1252")]
    public async Task ReadFromUtf8JsonAsync_IgnoresTheCharset(string charset)
    {
        using var content = Body("""{"name":"Projekt Đ"}""", "application/json", charset);

        var result = await content.ReadFromUtf8JsonAsync<NameOnly>(new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.Equal("Projekt Đ", result!.Name);
    }

    [Theory]
    [InlineData("utf-7")]
    [InlineData("windows-1252")]
    public async Task AzureDevOpsClient_FailedReadWithUndecodableBody_KeepsTheStatus(string charset)
    {
        _handler.EnqueueResponse(() => Response(HttpStatusCode.InternalServerError, "boom", "text/plain", charset));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => AzureDevOpsClient().GetProjectsAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [Theory]
    [InlineData("utf-7")]
    [InlineData("windows-1252")]
    public async Task AzureDevOpsClient_FailedCreateWithUndecodableBody_KeepsTheStatus(string charset)
    {
        _handler.EnqueueResponse(() => Response(HttpStatusCode.BadRequest, "bad", "text/plain", charset));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            AzureDevOpsClient().CreateWorkItemAsync("Project", "Task", new Dictionary<string, object> { ["System.Title"] = "x" }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task AzureDevOpsClient_JsonDeclaredInUtf7_IsRead()
    {
        _handler.EnqueueResponse(() => Response(
            HttpStatusCode.OK, """{"count":1,"value":[{"id":"p1","name":"Project"}]}""", "application/json", "utf-7"));

        var projects = await AzureDevOpsClient().GetProjectsAsync();

        Assert.Equal("Project", Assert.Single(projects).Name);
    }

    [Fact]
    public async Task AzureDevOpsTaskProvider_JsonDeclaredInUtf7_IsRead()
    {
        var provider = new AzureDevOpsTaskProvider(
            new MockHttpClientFactory(_handler, AzureDevOpsTaskProvider.HttpClientName),
            NullLogger<AzureDevOpsTaskProvider>.Instance,
            Options.Create(new AzureDevOpsOptions { ApiVersion = "7.1" }));
        _handler.EnqueueResponse(() => Response(
            HttpStatusCode.OK, """{"count":1,"value":[{"id":"p1","name":"Project"}]}""", "application/json", "utf-7"));

        var projects = await provider.GetProjectsAsync("pat", "https://dev.azure.com/myorg/");

        Assert.Equal("Project", Assert.Single(projects).Name);
    }

    [Theory]
    [InlineData("utf-7")]
    [InlineData("windows-1252")]
    public async Task MondayTaskProvider_UnauthorizedWithUndecodableBody_ReturnsFalse(string charset)
    {
        _handler.EnqueueResponse(() => Response(
            HttpStatusCode.Unauthorized,
            """{"errors":[{"message":"Not authenticated","extensions":{"code":"NOT_AUTHENTICATED"}}]}""",
            "application/json",
            charset));

        Assert.False(await MondayProvider().ValidateConnectionAsync("token"));
    }

    [Theory]
    [InlineData("utf-7")]
    [InlineData("windows-1252")]
    public async Task MondayTaskProvider_ForbiddenWithRejectionCodeInUndecodableBody_ReturnsFalse(string charset)
    {
        _handler.EnqueueResponse(() => Response(
            HttpStatusCode.Forbidden,
            """{"errors":[{"message":"Denied","extensions":{"code":"USER_ACCESS_DENIED"}}]}""",
            "application/json",
            charset));

        Assert.False(await MondayProvider().ValidateConnectionAsync("token"));
    }

    [Theory]
    [InlineData("utf-7")]
    [InlineData("windows-1252")]
    public async Task GitHubTaskProvider_RateLimitMessageInUndecodableBody_StillReadsAsARateLimit(string charset)
    {
        var provider = new GitHubTaskProvider(
            new MockHttpClientFactory(_handler, GitHubTaskProvider.HttpClientName, new Uri("https://api.github.com/")),
            NullLogger<GitHubTaskProvider>.Instance);
        _handler.EnqueueResponse(() => Response(
            HttpStatusCode.Forbidden, """{"message":"You have exceeded a secondary rate limit."}""", "application/json", charset));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => provider.SearchTasksAsync("token", "bug"));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
    }

    [Fact]
    public async Task TrelloTaskProvider_JsonDeclaredInUtf7_IsRead()
    {
        var provider = new TrelloTaskProvider(
            new MockHttpClientFactory(_handler, TrelloTaskProvider.HttpClientName, new Uri("https://api.trello.com/1/")),
            NullLogger<TrelloTaskProvider>.Instance);
        _handler.EnqueueResponse(() => Response(
            HttpStatusCode.OK, """[{"id":"b1","name":"Board"}]""", "application/json", "utf-7"));

        var boards = await provider.GetProjectsAsync("key:token");

        Assert.Equal("Board", Assert.Single(boards).Name);
    }

    [Fact]
    public async Task ClickUpTaskProvider_JsonDeclaredInUtf7_IsRead()
    {
        var provider = new ClickUpTaskProvider(
            new MockHttpClientFactory(_handler, ClickUpTaskProvider.HttpClientName, new Uri("https://api.clickup.com/api/v2/")),
            NullLogger<ClickUpTaskProvider>.Instance);
        _handler.EnqueueResponse(() => Response(
            HttpStatusCode.OK, """{"teams":[{"id":"t1","name":"Team"}]}""", "application/json", "utf-7"));

        var teams = await provider.GetProjectsAsync("pk_test_token");

        Assert.Equal("Team", Assert.Single(teams).Name);
    }

    [Fact]
    public async Task GetFromUtf8JsonAsync_FailedResponse_ThrowsWithTheStatus()
    {
        _handler.EnqueueResponse(() => Response(HttpStatusCode.NotFound, "missing", "text/plain", "utf-7"));
        using var client = new HttpClient(_handler) { BaseAddress = new Uri("https://example.test/") };

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetFromUtf8JsonAsync<NameOnly>("x", null));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    private AzureDevOpsClient AzureDevOpsClient() => new(
        new HttpClient(_handler) { BaseAddress = new Uri("https://dev.azure.com/myorg/") },
        Options.Create(new AzureDevOpsOptions { ApiVersion = "7.1" }),
        NullLogger<AzureDevOpsClient>.Instance);

    private MondayTaskProvider MondayProvider() => new(
        new MockHttpClientFactory(_handler, MondayTaskProvider.HttpClientName, new Uri("https://api.monday.com/v2")),
        NullLogger<MondayTaskProvider>.Instance);

    private static HttpResponseMessage Response(HttpStatusCode status, string body, string mediaType, string charset) =>
        new(status) { Content = Body(body, mediaType, charset) };

    private static ByteArrayContent Body(string body, string mediaType, string charset)
    {
        var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType) { CharSet = charset };
        return content;
    }

    private sealed record NameOnly(string Name);
}
