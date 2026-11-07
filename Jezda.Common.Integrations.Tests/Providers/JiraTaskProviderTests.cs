using System.Text;
using System.Net;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.Jira.Providers;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Providers;

public class JiraTaskProviderTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly JiraTaskProvider _provider;

    public JiraTaskProviderTests()
    {
        // No base address — JiraTaskProvider sets it per-request from baseUrl parameter
        var factory = new MockHttpClientFactory(_handler, JiraTaskProvider.HttpClientName);
        _provider = new JiraTaskProvider(factory, NullLogger<JiraTaskProvider>.Instance);
    }

    [Fact]
    public void Provider_ReturnsJira()
    {
        Assert.Equal(ExternalProvider.Jira, _provider.Provider);
    }

    [Fact]
    public void FormatAccessToken_CombinesEmailAndToken()
    {
        var result = JiraTaskProvider.FormatAccessToken("user@example.com", "api-token-123");
        Assert.Equal("user@example.com:api-token-123", result);
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithValidToken_ReturnsTrue()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { accountId = "123", displayName = "Test User" });

        var result = await _provider.ValidateConnectionAsync("user@test.com:token", "https://test.atlassian.net/");

        Assert.True(result);
        Assert.Single(_handler.SentRequests);
        Assert.Equal("Basic", _handler.SentRequests[0].Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task ValidateConnectionAsync_NonAsciiEmail_SendsItAsUtf8()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new { accountId = "123" });

        await _provider.ValidateConnectionAsync("\u017Eika@example.com:token", "https://test.atlassian.net/");

        var sent = Convert.FromBase64String(_handler.SentRequests[0].Headers.Authorization!.Parameter!);
        Assert.Equal("\u017Eika@example.com:token", Encoding.UTF8.GetString(sent));
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithInvalidToken_ReturnsFalse()
    {
        _handler.EnqueueResponse(HttpStatusCode.Unauthorized);

        var result = await _provider.ValidateConnectionAsync("bad:token", "https://test.atlassian.net/");

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateConnectionAsync_Forbidden_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueResponse(HttpStatusCode.Forbidden);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("user:token", "https://test.atlassian.net/"));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Fact]
    public async Task ValidateConnectionAsync_FailedLoginHeader_ReturnsFalse()
    {
        _handler.EnqueueResponse(() => WithLoginReason(HttpStatusCode.Unauthorized, "AUTHENTICATED_FAILED"));

        var result = await _provider.ValidateConnectionAsync("bad:token", "https://test.atlassian.net/");

        Assert.False(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task ValidateConnectionAsync_Captcha_ThrowsInsteadOfBlamingTheToken(HttpStatusCode status)
    {
        // Jira refused without checking the credentials, so they may well be valid.
        _handler.EnqueueResponse(() => WithLoginReason(status, "AUTHENTICATION_DENIED"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("user:token", "https://test.atlassian.net/"));

        Assert.Equal(status, ex.StatusCode);
        Assert.Contains("CAPTCHA", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ValidateConnectionAsync_OtherFailureStatus_ThrowsInsteadOfBlamingTheToken(HttpStatusCode status)
    {
        _handler.EnqueueResponse(status);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("user:token", "https://test.atlassian.net/"));

        Assert.Equal(status, ex.StatusCode);
    }

    [Fact]
    public async Task ValidateConnectionAsync_ConnectionFailure_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueException(new HttpRequestException("Connection refused"));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("user:token", "https://test.atlassian.net/"));
    }

    [Fact]
    public async Task ValidateConnectionAsync_Timeout_ThrowsInsteadOfBlamingTheToken()
    {
        _handler.EnqueueException(new TaskCanceledException("The request timed out"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _provider.ValidateConnectionAsync("user:token", "https://test.atlassian.net/"));
    }

    [Fact]
    public async Task ValidateConnectionAsync_Redirect_ThrowsWithTheStatusAndTarget()
    {
        // .NET drops Authorization on a followed redirect, so following it would blame a valid token.
        _handler.EnqueueResponse(() => Redirect("https://new.atlassian.net/rest/api/3/myself"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("user:token", "https://old.atlassian.net/"));

        Assert.Equal(HttpStatusCode.Found, ex.StatusCode);
        Assert.Contains("https://old.atlassian.net/rest/api/3/myself", ex.Message);
        Assert.Contains("https://new.atlassian.net/rest/api/3/myself", ex.Message);
    }

    [Fact]
    public async Task ValidateConnectionAsync_RelativeRedirect_NamesTheResolvedTarget()
    {
        _handler.EnqueueResponse(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
            response.Headers.Location = new Uri("/jira/rest/api/3/myself", UriKind.Relative);
            return response;
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("user:token", "https://jira.example.com/"));

        Assert.Equal(HttpStatusCode.MovedPermanently, ex.StatusCode);
        Assert.Contains("https://jira.example.com/jira/rest/api/3/myself", ex.Message);
    }

    [Fact]
    public async Task ValidateConnectionAsync_RedirectWithoutLocation_StillThrows()
    {
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.Found));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("user:token", "https://test.atlassian.net/"));

        Assert.Equal(HttpStatusCode.Found, ex.StatusCode);
        Assert.Contains("(no location)", ex.Message);
    }

    [Fact]
    public async Task ValidateConnectionAsync_Redirect_KeepsCredentialsAndQueryOutOfTheMessage()
    {
        // A consumer that logs the exception must not log a password typed into the base URL, or the
        // opaque context a sign-in redirect carries.
        _handler.EnqueueResponse(() => Redirect("https://admin:location-secret@sso.example.com/login?state=opaque"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("user:token", "https://admin:base-secret@jira.example.com/"));

        Assert.DoesNotContain("secret", ex.Message);
        Assert.DoesNotContain("opaque", ex.Message);
        Assert.Contains("https://jira.example.com/rest/api/3/myself", ex.Message);
        Assert.Contains("https://sso.example.com/login", ex.Message);
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/xhtml+xml")]
    [InlineData("text/plain")]
    [InlineData(null)]
    public async Task ValidateConnectionAsync_SuccessWithoutJson_ThrowsBecauseTheTokenWasNotChecked(string? mediaType)
    {
        // An SSO login page or a site that is not Jira answers 200 whatever the token is.
        _handler.EnqueueResponse(() => Page(mediaType));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.ValidateConnectionAsync("user:token", "https://test.atlassian.net/"));

        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
        Assert.Contains("not checked", ex.Message);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("APPLICATION/JSON")]
    [InlineData("application/vnd.api+json")]
    public async Task ValidateConnectionAsync_AnyJsonType_ReturnsTrue(string mediaType)
    {
        _handler.EnqueueResponse(() => Page(mediaType));

        Assert.True(await _provider.ValidateConnectionAsync("user:token", "https://test.atlassian.net/"));
    }

    [Fact]
    public async Task ValidateConnectionAsync_UnauthorizedWithHtml_ReturnsFalse()
    {
        // A failure status is an answer about the token, whatever the body looks like.
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("<html></html>", System.Text.Encoding.UTF8, "text/html")
        });

        Assert.False(await _provider.ValidateConnectionAsync("bad:token", "https://test.atlassian.net/"));
    }

    [Fact]
    public async Task GetProjectsAsync_Redirect_ThrowsWithTheStatusAndTarget()
    {
        _handler.EnqueueResponse(() => Redirect("https://new.atlassian.net/rest/api/3/project"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.GetProjectsAsync("user:token", "https://old.atlassian.net/"));

        Assert.Equal(HttpStatusCode.Found, ex.StatusCode);
        Assert.Contains("https://new.atlassian.net/rest/api/3/project", ex.Message);
    }

    [Fact]
    public async Task GetProjectsAsync_HtmlOnSuccess_ThrowsHttpRequestException()
    {
        _handler.EnqueueResponse(() => Page("text/html"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.GetProjectsAsync("user:token", "https://test.atlassian.net/"));

        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
    }

    [Fact]
    public async Task GetProjectsAsync_FailureStatus_ThrowsWithThatStatus()
    {
        _handler.EnqueueResponse(HttpStatusCode.Forbidden);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.GetProjectsAsync("user:token", "https://test.atlassian.net/"));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Fact]
    public async Task GetTasksAsync_Redirect_ThrowsWithTheStatusAndTarget()
    {
        _handler.EnqueueResponse(() => Redirect("https://new.atlassian.net/rest/api/3/search"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.GetTasksAsync("user:token", "PROJ", "https://old.atlassian.net/"));

        Assert.Equal(HttpStatusCode.Found, ex.StatusCode);
        Assert.Contains("https://new.atlassian.net/rest/api/3/search", ex.Message);
    }

    [Fact]
    public async Task GetTasksAsync_HtmlOnSuccess_ThrowsHttpRequestException()
    {
        _handler.EnqueueResponse(() => Page("text/html"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _provider.GetTasksAsync("user:token", "PROJ", "https://test.atlassian.net/"));

        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
    }

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri(location);
        return response;
    }

    private static HttpResponseMessage Page(string? mediaType)
    {
        var content = new ByteArrayContent("{}"u8.ToArray());

        if (mediaType is not null)
        {
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        }

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static HttpResponseMessage WithLoginReason(HttpStatusCode status, string reason)
    {
        var response = new HttpResponseMessage(status);
        response.Headers.Add("X-Seraph-LoginReason", reason);
        return response;
    }

    [Fact]
    public async Task ValidateConnectionAsync_WithoutBaseUrl_ThrowsArgumentException()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            _provider.ValidateConnectionAsync("token", baseUrl: null));
    }

    [Fact]
    public async Task GetProjectsAsync_ReturnsExternalProjectDtos()
    {
        var projects = new[]
        {
            new { id = "10001", key = "PROJ", name = "Project One" },
            new { id = "10002", key = "TEST", name = "Test Project" }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, projects);

        var result = await _provider.GetProjectsAsync("user@test.com:token", "https://test.atlassian.net/");

        Assert.Equal(2, result.Count);
        Assert.Equal("10001", result[0].Id);
        Assert.Equal("PROJ", result[0].Path);
        Assert.Equal("Project One", result[0].Name);
        Assert.Equal("https://test.atlassian.net/browse/PROJ", result[0].Url);
        Assert.Equal(ExternalProvider.Jira, result[0].Provider);
    }

    [Fact]
    public async Task GetProjectsAsync_BaseUrlWithCredentials_LeavesThemOutOfTheLink()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new[] { new { id = "10001", key = "PROJ", name = "Project One" } });

        var result = await _provider.GetProjectsAsync("user@test.com:token", "https://admin:base-secret@test.atlassian.net/jira");

        Assert.Equal("https://test.atlassian.net/jira/browse/PROJ", Assert.Single(result).Url);
    }

    [Fact]
    public async Task GetTasksAsync_BaseUrlWithCredentials_LeavesThemOutOfTheLink()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            issues = new[] { new { id = "10001", key = "PROJ-1", fields = new { summary = "Fix bug" } } }
        });

        var result = await _provider.GetTasksAsync("user@test.com:token", "PROJ", "https://admin:base-secret@test.atlassian.net/");

        Assert.Equal("https://test.atlassian.net/browse/PROJ-1", Assert.Single(result).Url);
    }

    [Fact]
    public async Task GetTasksAsync_ReturnsExternalTaskDtos()
    {
        var searchResponse = new
        {
            startAt = 0,
            maxResults = 50,
            total = 2,
            issues = new[]
            {
                new { id = "10001", key = "PROJ-1", fields = new { summary = "Fix bug", status = new { name = "In Progress" } } },
                new { id = "10002", key = "PROJ-2", fields = new { summary = "Add feature", status = new { name = "To Do" } } }
            }
        };
        _handler.EnqueueResponse(HttpStatusCode.OK, searchResponse);

        var result = await _provider.GetTasksAsync("user@test.com:token", "PROJ", "https://test.atlassian.net/");

        Assert.Equal(2, result.Count);
        Assert.Equal("PROJ-1", result[0].Id);
        Assert.Equal("Fix bug", result[0].Title);
        Assert.Equal("In Progress", result[0].Status);
        Assert.Equal("https://test.atlassian.net/browse/PROJ-1", result[0].Url);
        Assert.Equal("PROJ", result[0].ProjectId);
        Assert.Equal(ExternalProvider.Jira, result[0].Provider);
    }

    [Fact]
    public async Task GetProjectsAsync_MapsWhatTheListReports()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new object[]
        {
            new { id = "1", key = "A", name = "Reported", archived = true, isPrivate = false },
            new { id = "2", key = "B", name = "Silent" }
        });

        var result = await _provider.GetProjectsAsync("user@test.com:token", "https://test.atlassian.net/");

        Assert.True(result[0].IsArchived);
        // Jira's isPrivate means the caller cannot see the project, which is not what the DTO says.
        Assert.Null(result[0].IsPrivate);
        Assert.Null(result[1].IsArchived);
        Assert.Null(result[1].IsPrivate);
        Assert.Null(result[0].CreatedAt);
        Assert.Null(result[0].UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetTasksAsync_BlankProject_ThrowsWithoutCallingJira(string projectId)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _provider.GetTasksAsync("user@test.com:token", projectId, "https://test.atlassian.net/"));

        Assert.Empty(_handler.SentRequests);
    }

    [Theory]
    [InlineData("10001", "project = 10001")]
    [InlineData("PROJ", "project = \"PROJ\"")]
    public async Task GetTasksAsync_ReadsTheProjectOffEachIssue(string projectId, string expectedJql)
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, new
        {
            issues = new[]
            {
                new
                {
                    id = "1",
                    key = "PROJ-1",
                    fields = new
                    {
                        summary = "Fix bug",
                        status = new { name = "To Do" },
                        project = new { id = "10001", key = "PROJ", name = "Project One" }
                    }
                }
            }
        });

        var result = await _provider.GetTasksAsync("user@test.com:token", projectId, "https://test.atlassian.net/");

        var task = Assert.Single(result);
        Assert.Equal("10001", task.ProjectId);

        using var body = System.Text.Json.JsonDocument.Parse(await _handler.SentRequests[0].Content!.ReadAsStringAsync());
        Assert.Equal(expectedJql, body.RootElement.GetProperty("jql").GetString());
        Assert.Contains("project", body.RootElement.GetProperty("fields").EnumerateArray().Select(f => f.GetString()));
    }
}
