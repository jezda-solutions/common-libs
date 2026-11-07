using System.Text;
using System.Net;
using System.Text.Json;
using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.AzureDevOps.Configuration;
using Jezda.Common.Integrations.AzureDevOps.Providers;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Providers;

public class AzureDevOpsTaskProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly AzureDevOpsTaskProvider _provider;

    public AzureDevOpsTaskProviderTests()
    {
        var factory = new MockHttpClientFactory(_handler, AzureDevOpsTaskProvider.HttpClientName);
        var options = Options.Create(new AzureDevOpsOptions { ApiVersion = "7.1" });
        _provider = new AzureDevOpsTaskProvider(factory, NullLogger<AzureDevOpsTaskProvider>.Instance, options);
    }

    [Fact]
    public void Provider_ReturnsAzureDevOps()
    {
        Assert.Equal(ExternalProvider.AzureDevOps, _provider.Provider);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithValidToken_ReturnsTrue()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 1, value = new[] { new { id = "proj-1", name = "Project" } } });
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 0, value = Array.Empty<object>() });

        var result = await _provider.ValidateConnectionAsync("my-pat", "https://dev.azure.com/myorg/");

        Assert.True(result);
        Assert.Equal(2, _handler.SentRequests.Count);
        Assert.All(_handler.SentRequests, request => Assert.Equal("Basic", request.Headers.Authorization?.Scheme));
    }

    [Fact]
    public async Task ValidateConnectionAsync_ChecksProjectsThenWorkItemFieldsForTheOrganisation()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 0, value = Array.Empty<object>() });
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 0, value = Array.Empty<object>() });

        await _provider.ValidateConnectionAsync("my-pat", "https://dev.azure.com/myorg/");

        Assert.Equal("/myorg/_apis/projects", _handler.SentRequests[0].RequestUri!.AbsolutePath);
        Assert.Equal("/myorg/_apis/wit/fields", _handler.SentRequests[1].RequestUri!.AbsolutePath);
        Assert.Equal("?api-version=7.1", _handler.SentRequests[1].RequestUri!.Query);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ValidateConnectionAsync_WorkItemsRefused_ReturnsFalse(HttpStatusCode status)
    {
        // A PAT with "Project and Team (Read)" only: it lists projects, then fails on the first sync.
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 0, value = Array.Empty<object>() });
        _handler.EnqueueResponse(status);

        var result = await _provider.ValidateConnectionAsync("project-only-pat", "https://dev.azure.com/myorg/");

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateConnectionAsync_SignInPageOnWorkItems_ReturnsFalse()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 0, value = Array.Empty<object>() });
        _handler.EnqueueResponse(() => SignInPage(HttpStatusCode.NonAuthoritativeInformation));

        var result = await _provider.ValidateConnectionAsync("project-only-pat", "https://dev.azure.com/myorg/");

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateConnectionAsync_ServerErrorOnWorkItems_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 0, value = Array.Empty<object>() });
        _handler.EnqueueResponse(HttpStatusCode.ServiceUnavailable);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
    }

    [Fact]
    public async Task ValidateConnectionAsync_NonAsciiToken_SendsItAsUtf8()
    {
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        await _provider.ValidateConnectionAsync("pat\u201D", "https://dev.azure.com/myorg/");

        var sent = Convert.FromBase64String(_handler.SentRequests[0].Headers.Authorization!.Parameter!);
        Assert.Equal(":pat\u201D", Encoding.UTF8.GetString(sent));
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithInvalidToken_ReturnsFalse()
    {
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        var result = await _provider.ValidateConnectionAsync("bad-pat", "https://dev.azure.com/myorg/");

        Assert.False(result);
        Assert.Single(_handler.SentRequests);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithoutBaseUrl_ThrowsArgumentException()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            _provider.ValidateConnectionAsync("pat", baseUrl: null));
    }

    [Fact]
    public async Task ValidateConnectionAsync_SignInPage_ReturnsFalse()
    {
        // What a bad PAT gets when the FedAuthRedirect header is dropped on the way or ignored.
        _handler.EnqueueResponse(() => SignInPage(HttpStatusCode.NonAuthoritativeInformation));

        var result = await _provider.ValidateConnectionAsync("bad-pat", "https://dev.azure.com/myorg/");

        Assert.False(result);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/xhtml+xml")]
    [InlineData("application/xml")]
    [InlineData(null)]
    public async Task ValidateConnectionAsync_SuccessWithoutJson_ThrowsInsteadOfAcceptingTheToken(string? mediaType)
    {
        _handler.EnqueueResponse(() => WithBody(HttpStatusCode.OK, mediaType));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/"));

        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
        Assert.Contains(mediaType ?? "no content type", ex.Message);
    }

    [Fact]
    public async Task GetProjectsAsync_SuccessWithoutJson_ThrowsWithTheStatus()
    {
        _handler.EnqueueResponse(() => WithBody(HttpStatusCode.OK, "text/plain"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.GetProjectsAsync("pat", "https://dev.azure.com/myorg/"));

        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/problem+json")]
    public async Task ValidateConnectionAsync_SuccessWithJsonType_AcceptsTheToken(string mediaType)
    {
        _handler.EnqueueResponse(() => WithBody(HttpStatusCode.OK, mediaType));
        _handler.EnqueueResponse(() => WithBody(HttpStatusCode.OK, mediaType));

        Assert.True(await _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/"));
    }

    [Fact]
    public async Task ValidateConnectionAsync_NoContent_IsNotReadAsAMissingJsonType()
    {
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.NoContent));
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.NoContent));

        Assert.True(await _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/"));
    }

    [Fact]
    public async Task ValidateConnectionAsync_OkWithEmptyBody_IsNotReadAsAMissingJsonType()
    {
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) });
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) });

        Assert.True(await _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/"));
    }

    private static HttpResponseMessage WithBody(HttpStatusCode statusCode, string? mediaType)
    {
        var content = new ByteArrayContent("{}"u8.ToArray());
        content.Headers.ContentType = mediaType is null ? null : new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);

        return new HttpResponseMessage(statusCode) { Content = content };
    }

    [Fact]
    public async Task ValidateConnectionAsync_HtmlOnOk_ThrowsInsteadOfBlamingTheToken()
    {
        // A base URL pointing at a website or a captive portal: the token was never checked.
        _handler.EnqueueResponse(() => SignInPage(HttpStatusCode.OK));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/"));

        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
        Assert.Contains("text/html", ex.Message);
    }

    [Fact]
    public async Task ValidateConnectionAsync_Forbidden_ReturnsFalse()
    {
        _handler.EnqueueResponse(HttpStatusCode.Forbidden);

        var result = await _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/");

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateConnectionAsync_ServerError_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueResponse(HttpStatusCode.ServiceUnavailable);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
    }

    [Fact]
    public async Task ValidateConnectionAsync_ConnectionFailure_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueException(new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/"));
    }

    [Fact]
    public async Task ValidateConnectionAsync_Timeout_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueException(new TaskCanceledException("The request timed out"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/"));
    }

    [Fact]
    public async Task ValidateConnectionAsync_Redirect_ThrowsWithTheRedirectStatusAndTarget()
    {
        // A renamed organisation. Followed, the redirect would lose the Authorization header and
        // answer 401 for a valid PAT.
        _handler.EnqueueResponse(() => Redirect("https://dev.azure.com/neworg/_apis/projects"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/oldorg/"));

        Assert.Equal(HttpStatusCode.Found, ex.StatusCode);
        Assert.Contains("https://dev.azure.com/neworg/_apis/projects", ex.Message);
    }

    // The shape Azure DevOps answers a bad PAT with when the FedAuthRedirect header is missing.
    private const string SignInRedirect =
        "https://spsprodwus24.vssps.visualstudio.com/_signin?realm=dev.azure.com&reply_to=https%3A%2F%2Fdev.azure.com%2Fmyorg&redirect=1";

    [Fact]
    public async Task ValidateConnectionAsync_RedirectToSignIn_ReturnsFalse()
    {
        _handler.EnqueueResponse(() => Redirect(SignInRedirect));

        var result = await _provider.ValidateConnectionAsync("bad-pat", "https://dev.azure.com/myorg/");

        Assert.False(result);
    }

    [Fact]
    public async Task GetProjectsAsync_RedirectToSignIn_ThrowsUnauthorized()
    {
        _handler.EnqueueResponse(() => Redirect(SignInRedirect));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.GetProjectsAsync("bad-pat", "https://dev.azure.com/myorg/"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task ValidateConnectionAsync_NonAuthoritativeJson_ReturnsTrue()
    {
        // A transforming proxy can answer 203 with the real response. Only JSON counts as an answer.
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.NonAuthoritativeInformation)
        {
            Content = new StringContent("""{"count":0,"value":[]}""", System.Text.Encoding.UTF8, "application/json")
        });
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 0, value = Array.Empty<object>() });

        var result = await _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/");

        Assert.True(result);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData(null)]
    public async Task ValidateConnectionAsync_NonAuthoritativeWithoutJson_ReturnsFalse(string? mediaType)
    {
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.NonAuthoritativeInformation)
        {
            Content = mediaType is null
                ? new ByteArrayContent([])
                : new StringContent("Sign in", System.Text.Encoding.UTF8, mediaType)
        });

        var result = await _provider.ValidateConnectionAsync("bad-pat", "https://dev.azure.com/myorg/");

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateConnectionAsync_DisposesBothResponses()
    {
        var projects = new TrackingContent();
        var fields = new TrackingContent();
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = projects });
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = fields });

        await _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/");

        Assert.True(projects.Disposed);
        Assert.True(fields.Disposed);
    }

    [Fact]
    public async Task GetProjectsAsync_SignInPage_ThrowsUnauthorized()
    {
        _handler.EnqueueResponse(() => SignInPage(HttpStatusCode.NonAuthoritativeInformation));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.GetProjectsAsync("bad-pat", "https://dev.azure.com/myorg/"));

        // 401, so a caller that treats it as an expired token asks the user for a new one.
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task GetTasksAsync_SignInPageOnWiql_ThrowsUnauthorized()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { id = "proj-1", name = "Project" });
        _handler.EnqueueResponse(() => SignInPage(HttpStatusCode.NonAuthoritativeInformation));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.GetTasksAsync("bad-pat", "proj-1", "https://dev.azure.com/myorg/"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    // These two logs are what tells, from a real connection, whether _apis/wit/fields behaves as
    // documented: a token refused on it, or accepted and then refused on the first sync.
    [Fact]
    public async Task ValidateConnectionAsync_WorkItemsRefused_LogsTheStatusAndWhichRequest()
    {
        var logger = new RecordingLogger();
        var provider = ProviderWith(logger);
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 0, value = Array.Empty<object>() });
        _handler.EnqueueResponse(HttpStatusCode.Forbidden);

        await provider.ValidateConnectionAsync("project-only-pat", "https://dev.azure.com/myorg/");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Azure DevOps rejected the token with 403 on _apis/wit/fields for https://dev.azure.com/myorg/: {}", entry.Message);
    }

    [Fact]
    public async Task ValidateConnectionAsync_Forbidden_LogsTheBody()
    {
        // Conditional Access, an IP allow-list or a WAF answer 403 for a valid PAT; only the body says so.
        var logger = new RecordingLogger();
        var provider = ProviderWith(logger);
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("Blocked by IP policy " + new string('x', 1000))
        });

        await provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/");

        var message = Assert.Single(logger.Entries).Message;
        Assert.Contains("Blocked by IP policy", message);
        Assert.DoesNotContain(new string('x', 500), message);
    }

    [Fact]
    public async Task ValidateConnectionAsync_ForbiddenWithBodyInUnknownCharset_StillReturnsFalse()
    {
        // windows-1252 is one such charset: .NET cannot decode it without the CodePages provider.
        _handler.EnqueueResponse(() => WithUnknownCharset(HttpStatusCode.Forbidden));

        var result = await _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/");

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateConnectionAsync_ServerErrorWithBodyInUnknownCharset_KeepsTheStatus()
    {
        _handler.EnqueueResponse(() => WithUnknownCharset(HttpStatusCode.InternalServerError));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/"));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [Fact]
    public async Task ValidateConnectionAsync_ForbiddenWithLineBreaksInBody_LogsOneLine()
    {
        // The base URL comes from the user, so the body may be written to forge a log entry.
        var logger = new RecordingLogger();
        var provider = ProviderWith(logger);
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("denied\r\nfake log entry\ttabbed")
        });

        await provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/");

        var message = Assert.Single(logger.Entries).Message;
        Assert.DoesNotContain(message, char.IsControl);
        Assert.Contains("denied", message);
        Assert.Contains("fake log entry", message);
    }

    private static HttpResponseMessage WithUnknownCharset(HttpStatusCode statusCode)
    {
        var content = new ByteArrayContent("blocked"u8.ToArray());
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain") { CharSet = "windows-1252" };

        return new HttpResponseMessage(statusCode) { Content = content };
    }

    [Fact]
    public async Task ValidateConnectionAsync_SignInPage_LogsThatInsteadOfThePage()
    {
        var logger = new RecordingLogger();
        var provider = ProviderWith(logger);
        _handler.EnqueueResponse(() => SignInPage(HttpStatusCode.NonAuthoritativeInformation));

        await provider.ValidateConnectionAsync("pat", "https://dev.azure.com/myorg/");

        var message = Assert.Single(logger.Entries).Message;
        Assert.EndsWith(": a sign-in page", message);
        Assert.DoesNotContain("<html>", message);
    }

    [Fact]
    public async Task ValidateConnectionAsync_Accepted_LogsWithoutTheToken()
    {
        var logger = new RecordingLogger();
        var provider = ProviderWith(logger);
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 0, value = Array.Empty<object>() });
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 0, value = Array.Empty<object>() });

        await provider.ValidateConnectionAsync("my-pat", "https://dev.azure.com/myorg/");

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("Azure DevOps accepted the token for projects and work items on https://dev.azure.com/myorg/", entry.Message);
    }

    [Fact]
    public async Task ValidateConnectionAsync_BaseUrlWithCredentials_LeavesThemOutOfTheLog()
    {
        var logger = new RecordingLogger();
        var provider = ProviderWith(logger);
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        await provider.ValidateConnectionAsync("pat", "https://user:hunter2@dev.azure.com/myorg/");

        Assert.DoesNotContain("hunter2", Assert.Single(logger.Entries).Message);
    }

    [Fact]
    public async Task ValidateConnectionAsync_RedirectToSignInOnWorkItems_ReturnsFalse()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 0, value = Array.Empty<object>() });
        _handler.EnqueueResponse(() => Redirect(SignInRedirect));

        var result = await _provider.ValidateConnectionAsync("project-only-pat", "https://dev.azure.com/myorg/");

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateConnectionAsync_RedirectOnWorkItems_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 0, value = Array.Empty<object>() });
        _handler.EnqueueResponse(() => Redirect("https://dev.azure.com/neworg/_apis/wit/fields"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("pat", "https://dev.azure.com/oldorg/"));

        Assert.Equal(HttpStatusCode.Found, ex.StatusCode);
    }

    private AzureDevOpsTaskProvider ProviderWith(ILogger<AzureDevOpsTaskProvider> logger) => new(
        new MockHttpClientFactory(_handler, AzureDevOpsTaskProvider.HttpClientName),
        logger,
        Options.Create(new AzureDevOpsOptions { ApiVersion = "7.1" }));

    private sealed class RecordingLogger : ILogger<AzureDevOpsTaskProvider>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }

    private static HttpResponseMessage SignInPage(HttpStatusCode status) => new(status)
    {
        Content = new StringContent("<html><body>Sign in to your account</body></html>", System.Text.Encoding.UTF8, "text/html")
    };

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri(location);
        return response;
    }

    private sealed class TrackingContent() : StringContent("""{"count":0,"value":[]}""", System.Text.Encoding.UTF8, "application/json")
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    [Fact]
    public async Task GetProjectsAsync_ReturnsExternalProjectDtos()
    {
        var response = new
        {
            count = 2,
            value = new[]
            {
                new { id = "guid-1", name = "ProjectA", description = "First project", url = "https://dev.azure.com/myorg/ProjectA", state = "wellFormed", revision = 1L, visibility = "private", lastUpdateTime = "2025-01-01T00:00:00Z" },
                new { id = "guid-2", name = "ProjectB", description = "", url = "https://dev.azure.com/myorg/ProjectB", state = "wellFormed", revision = 2L, visibility = "public", lastUpdateTime = "2025-01-02T00:00:00Z" }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, response);

        var result = await _provider.GetProjectsAsync("my-pat", "https://dev.azure.com/myorg/");

        Assert.Equal(2, result.Count);
        Assert.Equal("guid-1", result[0].Id);
        Assert.Equal("ProjectA", result[0].Path);
        Assert.Equal("ProjectA", result[0].Name);
        Assert.Equal("First project", result[0].Description);
        Assert.Equal(ExternalProvider.AzureDevOps, result[0].Provider);
    }

    [Fact]
    public async Task GetTasksAsync_ReturnsExternalTaskDtos()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("ProjectA"));

        // WIQL response with work item IDs
        var wiqlResponse = new
        {
            queryType = "flat",
            workItems = new[]
            {
                new { id = 1, url = "https://dev.azure.com/myorg/_apis/wit/workitems/1" },
                new { id = 2, url = "https://dev.azure.com/myorg/_apis/wit/workitems/2" }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, wiqlResponse);

        // Batch work item details
        var workItemsResponse = new
        {
            count = 2,
            value = new[]
            {
                new
                {
                    id = 1,
                    url = "https://dev.azure.com/myorg/_apis/wit/workitems/1",
                    fields = new Dictionary<string, object>
                    {
                        ["System.Title"] = "Task One",
                        ["System.State"] = "Active"
                    }
                },
                new
                {
                    id = 2,
                    url = "https://dev.azure.com/myorg/_apis/wit/workitems/2",
                    fields = new Dictionary<string, object>
                    {
                        ["System.Title"] = "Task Two",
                        ["System.State"] = "New"
                    }
                }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, workItemsResponse);

        var result = await _provider.GetTasksAsync("my-pat", "ProjectA", "https://dev.azure.com/myorg/");

        Assert.Equal(2, result.Count);
        Assert.Equal("1", result[0].Id);
        Assert.Equal("Task One", result[0].Title);
        Assert.Equal("Active", result[0].Status);
        Assert.Equal("guid-ProjectA", result[0].ProjectId);
        Assert.Equal(ExternalProvider.AzureDevOps, result[0].Provider);

        // The page a person opens, not the REST resource the API returned as `url`.
        Assert.Equal("https://dev.azure.com/myorg/ProjectA/_workitems/edit/1", result[0].Url);
    }

    [Fact]
    public async Task GetProjectsAsync_Rejected_CarriesTheAzureDevOpsReasonAndStatus()
    {
        _handler.EnqueueResponse(
            HttpStatusCode.Forbidden,
            """{"message":"TF400813: The user is not authorized to access this resource."}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.GetProjectsAsync("my-pat", "https://dev.azure.com/myorg/"));

        Assert.Contains("TF400813", ex.Message);
        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Fact]
    public async Task GetTasksAsync_AsksTheDetailsBatchToOmitUnreadableItems()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("ProjectA"));
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1, 2));
        _handler.EnqueueResponse(HttpStatusCode.OK, Details((1, "One"), (2, "Two")));

        await _provider.GetTasksAsync("my-pat", "ProjectA", "https://dev.azure.com/myorg/");

        // Without it, one id deleted between the WIQL and this call fails the whole batch with 404.
        var detailsQuery = _handler.SentRequests[2].RequestUri!.Query;
        Assert.Contains("errorPolicy=omit", detailsQuery);
    }

    [Fact]
    public async Task GetTasksAsync_SkipsItemsTheDetailsBatchOmitted()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("ProjectA"));
        // With errorPolicy=omit, Azure DevOps answers an unreadable id with a null slot.
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1, 2));
        _handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"count":2,"value":[
              {"id":1,"url":"https://dev.azure.com/myorg/_apis/wit/workItems/1","fields":{"System.Title":"One","System.State":"Active"}},
              null
            ]}
            """);

        var result = await _provider.GetTasksAsync("my-pat", "ProjectA", "https://dev.azure.com/myorg/");

        Assert.Equal(["1"], result.Select(t => t.Id));
    }

    [Fact]
    public async Task GetTasksAsync_ItemsOmitted_LogsAWarningWithTheCounts()
    {
        var logger = new RecordingLogger<AzureDevOpsTaskProvider>();
        var provider = ProviderWith(logger);
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("ProjectA"));
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1, 2));
        _handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"count":2,"value":[
              {"id":1,"url":"https://dev.azure.com/myorg/_apis/wit/workItems/1","fields":{"System.Title":"One","System.State":"Active"}},
              null
            ]}
            """);

        await provider.GetTasksAsync("my-pat", "ProjectA", "https://dev.azure.com/myorg/");

        var warning = Assert.Single(logger.Warnings);
        Assert.Contains("returned 1 of 2", warning);
    }

    [Fact]
    public async Task SearchTasksAsync_SkipsItemsTheDetailsBatchOmitted()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(9, 3));
        _handler.EnqueueResponse(
            HttpStatusCode.OK,
            """
            {"count":2,"value":[
              null,
              {"id":3,"url":"https://dev.azure.com/myorg/_apis/wit/workItems/3","fields":{"System.Title":"Kept","System.State":"Active","System.TeamProject":"Platform"}}
            ]}
            """);
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("Platform"));

        var result = await _provider.SearchTasksAsync("my-pat", "webhook", baseUrl: "https://dev.azure.com/myorg/");

        Assert.Equal(["3"], result.Select(t => t.Id));
    }

    [Fact]
    public async Task GetTasksAsync_RejectedDetailsBatch_CarriesTheAzureDevOpsReasonAndStatus()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("ProjectA"));
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1));
        _handler.EnqueueResponse(
            HttpStatusCode.BadRequest,
            """{"message":"VS403474: The request contains too many work item ids."}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.GetTasksAsync("my-pat", "ProjectA", "https://dev.azure.com/myorg/"));

        Assert.Contains("VS403474", ex.Message);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task GetTasksAsync_EmptyWiqlResult_ReturnsEmpty()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("ProjectA"));
        var wiqlResponse = new { queryType = "flat", workItems = Array.Empty<object>() };
        _handler.EnqueueResponse(HttpStatusCode.OK, wiqlResponse);

        var result = await _provider.GetTasksAsync("my-pat", "ProjectA", "https://dev.azure.com/myorg/");

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchTasksAsync_QueriesTheWholeOrganisation()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { queryType = "flat", workItems = Array.Empty<object>() });

        // Through the interface on purpose: SearchTasksAsync is a default interface implementation
        // that this class overrides implicitly, with no `override` keyword and no diagnostic if the
        // signature drifts. Calling the concrete method would keep passing after the override had
        // silently stopped implementing the interface member.
        await ((IExternalTaskProvider)_provider).SearchTasksAsync("my-pat", "webhook", baseUrl: "https://dev.azure.com/myorg/");

        // No project segment in the path and no [System.TeamProject] in the WHERE clause: that is
        // what makes this organisation-wide rather than one query per project. The "myorg" segment
        // is the organisation, and comes from the base address — GetTasksAsync would add a project
        // segment after it.
        var request = Assert.Single(_handler.SentRequests);
        Assert.Equal("/myorg/_apis/wit/wiql", request.RequestUri!.AbsolutePath);

        var wiql = await ReadWiqlQueryAsync(request);
        Assert.Contains("[System.Title] CONTAINS 'webhook'", wiql);
        Assert.Contains("[System.State] <> 'Removed'", wiql);
        Assert.DoesNotContain("System.TeamProject", wiql);
    }

    [Fact]
    public async Task SearchTasksAsync_BoundsTheIdQueryServerSide()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { queryType = "flat", workItems = Array.Empty<object>() });

        await _provider.SearchTasksAsync("my-pat", "webhook", limit: 5, baseUrl: "https://dev.azure.com/myorg/");

        // Without a server-side bound, a common term against a large organisation returns thousands
        // of refs that are fetched, parsed, and then thrown away by the Take below. The bound is the
        // $top URL parameter: WIQL has no TOP keyword, and Azure DevOps rejects a query containing
        // one with 400 — which is how search failed on every call in production.
        var request = _handler.SentRequests[0];
        Assert.Contains("$top=5", request.RequestUri!.Query);
        Assert.Contains("api-version=", request.RequestUri!.Query);

        var wiql = await ReadWiqlQueryAsync(request);
        Assert.DoesNotContain("TOP", wiql);
    }

    [Fact]
    public async Task SearchTasksAsync_RejectedQuery_CarriesTheAzureDevOpsReasonAndStatus()
    {
        _handler.EnqueueResponse(
            HttpStatusCode.BadRequest,
            """{"message":"TF51006: The query statement is missing a FROM clause. The error is caused by «20»."}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.SearchTasksAsync("my-pat", "webhook", baseUrl: "https://dev.azure.com/myorg/"));

        // The reason is what makes the next rejected query diagnosable from the logs alone.
        Assert.Contains("TF51006", ex.Message);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task SearchTasksAsync_Unauthorized_KeepsTheStatusCode()
    {
        // The platform flags an integration for re-authentication by matching 401/403 on
        // HttpRequestException.StatusCode; carrying the body must not lose it.
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized, "");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.SearchTasksAsync("my-pat", "webhook", baseUrl: "https://dev.azure.com/myorg/"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task SearchTasksAsync_RestoresTheQueryOrderAfterTheDetailsBatch()
    {
        // WIQL sorts newest-changed first; `workitems?ids=` does not contract to answer in the
        // order the ids were supplied. Here it answers in the opposite order.
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            queryType = "flat",
            workItems = new object[]
            {
                new { id = 9, url = "https://dev.azure.com/myorg/_apis/wit/workItems/9" },
                new { id = 3, url = "https://dev.azure.com/myorg/_apis/wit/workItems/3" }
            }
        });
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            count = 2,
            value = new object[]
            {
                new
                {
                    id = 3,
                    url = "https://dev.azure.com/myorg/_apis/wit/workItems/3",
                    fields = new Dictionary<string, object>
                    {
                        ["System.Title"] = "Older",
                        ["System.State"] = "Active",
                        ["System.TeamProject"] = "Platform"
                    }
                },
                new
                {
                    id = 9,
                    url = "https://dev.azure.com/myorg/_apis/wit/workItems/9",
                    fields = new Dictionary<string, object>
                    {
                        ["System.Title"] = "Newest",
                        ["System.State"] = "Active",
                        ["System.TeamProject"] = "Platform"
                    }
                }
            }
        });
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("Platform"));

        var result = await _provider.SearchTasksAsync("my-pat", "webhook", baseUrl: "https://dev.azure.com/myorg/");

        Assert.Equal(["9", "3"], result.Select(t => t.Id));
    }

    [Fact]
    public async Task SearchTasksAsync_ReadsProjectFromTheWorkItemNotAParameter()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            queryType = "flat",
            workItems = new object[] { new { id = 7, url = "https://dev.azure.com/myorg/_apis/wit/workItems/7" } }
        });
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            count = 1,
            value = new object[]
            {
                new
                {
                    id = 7,
                    url = "https://dev.azure.com/myorg/_apis/wit/workItems/7",
                    fields = new Dictionary<string, object>
                    {
                        ["System.Title"] = "Fix webhook retry",
                        ["System.State"] = "Active",
                        ["System.TeamProject"] = "Platform"
                    }
                }
            }
        });
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("Platform"));

        var result = await _provider.SearchTasksAsync("my-pat", "webhook", baseUrl: "https://dev.azure.com/myorg/");

        var task = Assert.Single(result);
        Assert.Equal("7", task.Id);
        Assert.Equal("Fix webhook retry", task.Title);
        Assert.Equal("Active", task.Status);
        Assert.Equal("guid-Platform", task.ProjectId);
        Assert.Equal(ExternalProvider.AzureDevOps, task.Provider);
    }

    [Fact]
    public async Task GetTasksAsync_KeepsTheCollectionSegmentOfAnOnPremisesServer()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("ProjectA"));
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            queryType = "flat",
            workItems = new object[] { new { id = 5, url = "https://server/tfs/Collection/_apis/wit/workItems/5" } }
        });
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            count = 1,
            value = new object[]
            {
                new
                {
                    id = 5,
                    url = "https://server/tfs/Collection/_apis/wit/workItems/5",
                    fields = new Dictionary<string, object> { ["System.Title"] = "On-prem", ["System.State"] = "New" }
                }
            }
        });

        // No trailing slash: resolving against it would otherwise drop "Collection".
        var result = await _provider.GetTasksAsync("my-pat", "ProjectA", "https://server/tfs/Collection");

        Assert.Equal("https://server/tfs/Collection/ProjectA/_workitems/edit/5", Assert.Single(result).Url);
    }

    [Fact]
    public async Task SearchTasksAsync_WithoutAProject_KeepsTheItemsOwnLink()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            queryType = "flat",
            workItems = new object[] { new { id = 8, url = "https://dev.azure.com/myorg/_apis/wit/workItems/8" } }
        });
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            count = 1,
            value = new object[]
            {
                new
                {
                    id = 8,
                    url = "https://dev.azure.com/myorg/_apis/wit/workItems/8",
                    fields = new Dictionary<string, object> { ["System.Title"] = "No project", ["System.State"] = "New" }
                }
            }
        });

        var result = await _provider.SearchTasksAsync("my-pat", "project", baseUrl: "https://dev.azure.com/myorg/");

        var task = Assert.Single(result);
        Assert.Equal("https://dev.azure.com/myorg/_apis/wit/workItems/8", task.Url);
        Assert.Equal(string.Empty, task.ProjectId);
    }

    [Fact]
    public async Task SearchTasksAsync_LinksToTheWorkItemPageInItsOwnProject()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            queryType = "flat",
            workItems = new object[] { new { id = 9469, url = "https://dev.azure.com/myorg/_apis/wit/workItems/9469" } }
        });
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            count = 1,
            value = new object[]
            {
                new
                {
                    id = 9469,
                    url = "https://dev.azure.com/myorg/3d0b922f-ad9b-4a3b-8782-fb898190824a/_apis/wit/workItems/9469",
                    fields = new Dictionary<string, object>
                    {
                        ["System.Title"] = "Fix webhook retry",
                        ["System.State"] = "Active",
                        ["System.TeamProject"] = "Mobile Team"
                    }
                }
            }
        });
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("Mobile Team"));

        var result = await _provider.SearchTasksAsync("my-pat", "webhook", baseUrl: "https://dev.azure.com/myorg");

        var task = Assert.Single(result);
        Assert.Equal("https://dev.azure.com/myorg/Mobile%20Team/_workitems/edit/9469", task.Url);
    }

    [Fact]
    public async Task SearchTasksAsync_TrimsIdsBeforeFetchingDetails()
    {
        var workItems = Enumerable.Range(1, 10)
            .Select(n => (object)new { id = n, url = $"https://dev.azure.com/myorg/_apis/wit/workItems/{n}" })
            .ToArray();
        _handler.EnqueueResponse(HttpStatusCode.OK, new { queryType = "flat", workItems });
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 0, value = Array.Empty<object>() });

        await _provider.SearchTasksAsync("my-pat", "webhook", limit: 3, baseUrl: "https://dev.azure.com/myorg/");

        // The details call is the expensive one — it must not hydrate rows the caller discards.
        var detailsQuery = _handler.SentRequests[1].RequestUri!.Query;
        Assert.Contains("ids=1,2,3", Uri.UnescapeDataString(detailsQuery));
    }

    [Fact]
    public async Task SearchTasksAsync_EscapesQuotesInTheSearchTerm()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { queryType = "flat", workItems = Array.Empty<object>() });

        await _provider.SearchTasksAsync("my-pat", "it's broken", baseUrl: "https://dev.azure.com/myorg/");

        var wiql = await ReadWiqlQueryAsync(_handler.SentRequests[0]);
        Assert.Contains("CONTAINS 'it''s broken'", wiql);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchTasksAsync_BlankTerm_ReturnsEmptyWithoutQuerying(string term)
    {
        var result = await _provider.SearchTasksAsync("my-pat", term, baseUrl: "https://dev.azure.com/myorg/");

        Assert.Empty(result);
        Assert.Empty(_handler.SentRequests);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("#1234")]
    [InlineData(" #1234 ")]
    public async Task SearchTasksAsync_NumericTerm_LooksUpTheIdAndSearchesTheTitle(string term)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { queryType = "flat", workItems = Array.Empty<object>() });
        _handler.EnqueueResponse(HttpStatusCode.OK, new { queryType = "flat", workItems = Array.Empty<object>() });

        await _provider.SearchTasksAsync("my-pat", term, baseUrl: "https://dev.azure.com/myorg/");

        // Developers know the id better than the title; "#1234" is how Azure DevOps writes it. The id
        // lookup is its own query so the title query's $top cannot cut it off.
        Assert.Equal(2, _handler.SentRequests.Count);

        var idWiql = await ReadWiqlQueryAsync(_handler.SentRequests[0]);
        Assert.Contains("WHERE [System.Id] = 1234 AND [System.State] <> 'Removed'", idWiql);
        Assert.DoesNotContain("$top", _handler.SentRequests[0].RequestUri!.Query);

        var titleWiql = await ReadWiqlQueryAsync(_handler.SentRequests[1]);
        Assert.Contains("[System.Title] CONTAINS '1234'", titleWiql);
        Assert.DoesNotContain("System.Id] =", titleWiql);
    }

    [Theory]
    [InlineData("v2 login")]
    [InlineData("12a")]
    [InlineData("#")]
    [InlineData("99999999999")]
    public async Task SearchTasksAsync_NonIdTerm_SearchesTheTitleOnly(string term)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { queryType = "flat", workItems = Array.Empty<object>() });

        await _provider.SearchTasksAsync("my-pat", term, baseUrl: "https://dev.azure.com/myorg/");

        // Text, mixed text, a bare "#" and a number beyond int range are not ids: one query, no
        // [System.Id] clause, which for the last would otherwise be a WIQL Azure DevOps rejects.
        var request = Assert.Single(_handler.SentRequests);
        var wiql = await ReadWiqlQueryAsync(request);
        Assert.DoesNotContain("System.Id] =", wiql);
        Assert.Contains($"[System.Title] CONTAINS '{term.Trim()}'", wiql);
    }

    [Fact]
    public async Task SearchTasksAsync_ExactIdMatch_ComesFirst_AndIsNotRepeated()
    {
        // The title query orders by change date, so the item asked for by number can come back below
        // more recently changed items that only contain the digits in their title.
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1234));
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(5000, 1234));
        _handler.EnqueueResponse(HttpStatusCode.OK, Details((5000, "Follow-up to 1234"), (1234, "Login fails")));
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("Platform"));

        var result = await _provider.SearchTasksAsync("my-pat", "#1234", baseUrl: "https://dev.azure.com/myorg/");

        Assert.Equal(["1234", "5000"], result.Select(t => t.Id));
    }

    [Fact]
    public async Task SearchTasksAsync_ExactIdBeyondTheTitleWindow_IsStillReturnedFirst()
    {
        // "12" is in the titles of many newer items; with limit 2 the title query's $top keeps only
        // those, and the older #12 is found by the id lookup alone.
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(12));
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(9012, 8120));
        _handler.EnqueueResponse(HttpStatusCode.OK, Details((9012, "Release 2.12"), (12, "Old bug")));
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("Platform"));

        var result = await _provider.SearchTasksAsync("my-pat", "12", limit: 2, baseUrl: "https://dev.azure.com/myorg/");

        Assert.Equal(["12", "9012"], result.Select(t => t.Id));
    }

    [Fact]
    public async Task GetProjectsAsync_FollowsTheContinuationTokenAcrossPages()
    {
        _handler.EnqueueResponseWithHeader(
            new { count = 1, value = new[] { Project("First") } },
            "x-ms-continuationtoken",
            "token/1");
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 1, value = new[] { Project("Second") } });

        var result = await _provider.GetProjectsAsync("my-pat", "https://dev.azure.com/myorg/");

        Assert.Equal(["guid-First", "guid-Second"], result.Select(p => p.Id));
        Assert.Equal(["First", "Second"], result.Select(p => p.Path));
        Assert.Equal(2, _handler.SentRequests.Count);

        var first = _handler.SentRequests[0].RequestUri!.Query;
        Assert.Contains("$top=100", first);
        Assert.DoesNotContain("continuationToken", first);

        var second = _handler.SentRequests[1].RequestUri!.Query;
        Assert.Contains("$top=100", second);
        Assert.Contains("continuationToken=token%2F1", second);
    }

    [Fact]
    public async Task GetProjectsAsync_MapsMetadata_AndLinksToTheProjectPage()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            count = 1,
            value = new[]
            {
                new
                {
                    id = "guid-1",
                    name = "Mobile Team",
                    url = "https://dev.azure.com/myorg/_apis/projects/guid-1",
                    state = "wellFormed",
                    visibility = "private",
                    lastUpdateTime = "2025-01-02T03:04:05Z"
                }
            }
        });

        var project = Assert.Single(await _provider.GetProjectsAsync("my-pat", "https://dev.azure.com/myorg"));

        // The page a person opens, not the REST resource the API returned as `url`.
        Assert.Equal("https://dev.azure.com/myorg/Mobile%20Team", project.Url);
        Assert.Null(project.CreatedAt);
        Assert.Equal(DateTimeOffset.Parse("2025-01-02T03:04:05Z"), project.UpdatedAt);
        Assert.False(project.IsArchived);
        Assert.True(project.IsPrivate);
    }

    [Theory]
    [InlineData("wellFormed", false)]
    [InlineData("deleting", true)]
    [InlineData("deleted", true)]
    public async Task GetProjectsAsync_ReadsIsArchivedFromState(string state, bool expected)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 1, value = new[] { new { id = "g", name = "P", state } } });

        var project = Assert.Single(await _provider.GetProjectsAsync("my-pat", "https://dev.azure.com/myorg"));

        Assert.Equal(expected, project.IsArchived);
    }

    [Theory]
    [InlineData("private", true)]
    [InlineData("organization", true)]
    [InlineData("public", false)]
    public async Task GetProjectsAsync_ReadsIsPrivateFromVisibility(string visibility, bool expected)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 1, value = new[] { new { id = "g", name = "P", visibility } } });

        var project = Assert.Single(await _provider.GetProjectsAsync("my-pat", "https://dev.azure.com/myorg"));

        Assert.Equal(expected, project.IsPrivate);
    }

    [Fact]
    public async Task GetProjectsAsync_WhatTheProviderOmits_StaysNull()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { count = 1, value = new[] { new { id = "g", name = "P" } } });

        var project = Assert.Single(await _provider.GetProjectsAsync("my-pat", "https://dev.azure.com/myorg"));

        Assert.Null(project.UpdatedAt);
        Assert.Null(project.IsArchived);
        Assert.Null(project.IsPrivate);
    }

    [Fact]
    public async Task GetTasksAsync_ByGuid_QueriesByTheCurrentName()
    {
        // WIQL's [System.TeamProject] takes the name, which is why the GUID is resolved first.
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("Bob's Team", "guid-1"));
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1));
        _handler.EnqueueResponse(HttpStatusCode.OK, Details((1, "One")));

        var result = await _provider.GetTasksAsync("my-pat", "guid-1", "https://dev.azure.com/myorg/");

        Assert.Equal("/myorg/_apis/projects/guid-1", _handler.SentRequests[0].RequestUri!.AbsolutePath);
        Assert.Equal("/myorg/Bob%27s%20Team/_apis/wit/wiql", _handler.SentRequests[1].RequestUri!.AbsolutePath);

        var wiql = await ReadWiqlQueryAsync(_handler.SentRequests[1]);
        Assert.Contains("[System.TeamProject] = 'Bob''s Team'", wiql);

        var task = Assert.Single(result);
        Assert.Equal("guid-1", task.ProjectId);
        Assert.Equal("https://dev.azure.com/myorg/Bob%27s%20Team/_workitems/edit/1", task.Url);
    }

    [Fact]
    public async Task GetTasksAsync_ByName_StillReturnsTheGuid()
    {
        // A row stored by an older version holds the name and must keep working until migrated.
        _handler.EnqueueResponse(HttpStatusCode.OK, Project("Mobile Team", "guid-1"));
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1));
        _handler.EnqueueResponse(HttpStatusCode.OK, Details((1, "One")));

        var result = await _provider.GetTasksAsync("my-pat", "Mobile Team", "https://dev.azure.com/myorg/");

        Assert.Equal("/myorg/_apis/projects/Mobile%20Team", _handler.SentRequests[0].RequestUri!.AbsolutePath);
        Assert.Equal("guid-1", Assert.Single(result).ProjectId);
    }

    [Fact]
    public async Task GetTasksAsync_UnknownProject_CarriesTheStatusCode()
    {
        _handler.EnqueueResponse(HttpStatusCode.NotFound, """{"message":"TF200016: The project does not exist."}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.GetTasksAsync("my-pat", "guid-1", "https://dev.azure.com/myorg/"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetTasksAsync_BlankProject_ThrowsWithoutCallingAzureDevOps(string projectId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _provider.GetTasksAsync("my-pat", projectId, "https://dev.azure.com/myorg/"));

        Assert.Empty(_handler.SentRequests);
    }

    [Fact]
    public async Task SearchTasksAsync_LooksUpEachProjectOnce()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1, 2, 3));
        _handler.EnqueueResponse(HttpStatusCode.OK, DetailsIn((1, "Alpha"), (2, "Beta"), (3, "Alpha")));
        _handler.RespondTo("/myorg/_apis/projects/Alpha", HttpStatusCode.OK, Project("Alpha"));
        _handler.RespondTo("/myorg/_apis/projects/Beta", HttpStatusCode.OK, Project("Beta"));

        var result = await _provider.SearchTasksAsync("my-pat", "webhook", baseUrl: "https://dev.azure.com/myorg/");

        Assert.Equal(4, _handler.SentRequests.Count);
        Assert.Equal(["guid-Alpha", "guid-Beta", "guid-Alpha"], result.Select(t => t.ProjectId));
    }

    [Fact]
    public async Task SearchTasksAsync_ProjectThatAnswers404_DropsOnlyItsItems()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1, 2));
        _handler.EnqueueResponse(HttpStatusCode.OK, DetailsIn((1, "Gone"), (2, "Kept")));
        _handler.RespondTo("/myorg/_apis/projects/Gone", HttpStatusCode.NotFound);
        _handler.RespondTo("/myorg/_apis/projects/Kept", HttpStatusCode.OK, Project("Kept"));

        var result = await _provider.SearchTasksAsync("my-pat", "webhook", baseUrl: "https://dev.azure.com/myorg/");

        Assert.Equal("2", Assert.Single(result).Id);
    }

    [Fact]
    public async Task SearchTasksAsync_UnauthorizedProjectLookup_KeepsTheStatusCode()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1));
        _handler.EnqueueResponse(HttpStatusCode.OK, Details((1, "One")));
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized, "");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.SearchTasksAsync("my-pat", "webhook", baseUrl: "https://dev.azure.com/myorg/"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task SearchTasksAsync_ForbiddenProjectLookup_KeepsTheStatusCode()
    {
        // Unlike GitHub, a 403 here can mean a PAT without the scope, so hiding it would turn a
        // misconfigured integration into results that silently go missing.
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs(1));
        _handler.EnqueueResponse(HttpStatusCode.OK, Details((1, "One")));
        _handler.EnqueueResponse(HttpStatusCode.Forbidden, "");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.SearchTasksAsync("my-pat", "webhook", baseUrl: "https://dev.azure.com/myorg/"));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Fact]
    public async Task SearchTasksAsync_LimitsConcurrentLookups()
    {
        var items = Enumerable.Range(1, 12).Select(i => (Id: i, Project: $"P{i}")).ToArray();
        _handler.EnqueueResponse(HttpStatusCode.OK, Refs([.. items.Select(i => i.Id)]));
        _handler.EnqueueResponse(HttpStatusCode.OK, DetailsIn(items));

        foreach (var item in items)
        {
            _handler.RespondTo($"/myorg/_apis/projects/{item.Project}", HttpStatusCode.OK, Project(item.Project), TimeSpan.FromMilliseconds(50));
        }

        var result = await _provider.SearchTasksAsync("my-pat", "webhook", baseUrl: "https://dev.azure.com/myorg/");

        Assert.Equal(12, result.Count);
        Assert.InRange(_handler.MaxInFlight, 2, 5);
    }

    private static object Project(string name, string? id = null) => new
    {
        id = id ?? $"guid-{name}",
        name
    };

    /// <summary>One work item per entry, each in the named project.</summary>
    private static object DetailsIn(params (int Id, string Project)[] items) => new
    {
        count = items.Length,
        value = items.Select(i => new
        {
            id = i.Id,
            url = $"https://dev.azure.com/myorg/_apis/wit/workItems/{i.Id}",
            fields = new Dictionary<string, object>
            {
                ["System.Title"] = $"Item {i.Id}",
                ["System.State"] = "Active",
                ["System.TeamProject"] = i.Project
            }
        }).ToArray()
    };

    private static object Refs(params int[] ids) => new
    {
        queryType = "flat",
        workItems = ids.Select(id => new { id, url = $"https://dev.azure.com/myorg/_apis/wit/workItems/{id}" }).ToArray()
    };

    private static object Details(params (int Id, string Title)[] items) => new
    {
        count = items.Length,
        value = items.Select(i => new
        {
            id = i.Id,
            url = $"https://dev.azure.com/myorg/_apis/wit/workItems/{i.Id}",
            fields = new Dictionary<string, object>
            {
                ["System.Title"] = i.Title,
                ["System.State"] = "Active",
                ["System.TeamProject"] = "Platform"
            }
        }).ToArray()
    };

    /// <summary>
    /// Pulls the WIQL out of the request body. Asserting on the raw JSON does not work: the default
    /// <c>JavaScriptEncoder</c> escapes <c>'</c> and <c>&lt;&gt;</c> to <c>'</c> / <c><</c>,
    /// so the query reads nothing like what Azure DevOps receives after parsing.
    /// </summary>
    private static async Task<string> ReadWiqlQueryAsync(HttpRequestMessage request)
    {
        var body = await request.Content!.ReadAsStringAsync();

        return JsonDocument.Parse(body).RootElement.GetProperty("query").GetString()!;
    }
}
