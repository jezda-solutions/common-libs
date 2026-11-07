using System.Net;
using System.Text;
using Jezda.Common.Integrations.AzureDevOps;
using Jezda.Common.Integrations.AzureDevOps.Configuration;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Clients;

/// <summary>
/// Responses whose status alone misreports what happened: a sign-in page served as success, and a
/// redirect the client does not follow.
/// </summary>
public class AzureDevOpsClientAuthenticationTests
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly AzureDevOpsClient _client;

    public AzureDevOpsClientAuthenticationTests()
    {
        var httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://dev.azure.com/myorg/") };
        var options = Options.Create(new AzureDevOpsOptions { ApiVersion = "7.1" });
        _client = new AzureDevOpsClient(httpClient, options, NullLogger<AzureDevOpsClient>.Instance);
    }

    [Fact]
    public async Task GetProjectsAsync_SignInPage_ThrowsUnauthorized()
    {
        _handler.EnqueueResponse(() => SignInPage(HttpStatusCode.NonAuthoritativeInformation));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetProjectsAsync());

        // Not a JSON parsing error without a status: 401, which callers read as an expired token.
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task GetProjectsAsync_HtmlOnOk_ThrowsWithItsOwnStatusInsteadOfBlamingTheToken()
    {
        // A website or a captive portal. 401 here would have the user replace a valid PAT.
        _handler.EnqueueResponse(() => SignInPage(HttpStatusCode.OK));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetProjectsAsync());

        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
        Assert.Contains("text/html", ex.Message);
    }

    [Fact]
    public async Task GetWorkItemsByQueryAsync_SignInPageOnWiql_ThrowsUnauthorized()
    {
        _handler.EnqueueResponse(() => SignInPage(HttpStatusCode.NonAuthoritativeInformation));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems"));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task CreateWorkItemAsync_SignInPage_ThrowsUnauthorized()
    {
        _handler.EnqueueResponse(() => SignInPage(HttpStatusCode.NonAuthoritativeInformation));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _client.CreateWorkItemAsync("Project", "Task", new Dictionary<string, object> { ["System.Title"] = "x" }));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task GetProjectsAsync_Redirect_ThrowsWithTheRedirectStatusAndTarget()
    {
        _handler.EnqueueResponse(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
            response.Headers.Location = new Uri("https://dev.azure.com/neworg/_apis/projects");
            return response;
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetProjectsAsync());

        Assert.Equal(HttpStatusCode.MovedPermanently, ex.StatusCode);
        Assert.Contains("https://dev.azure.com/neworg/_apis/projects", ex.Message);
    }

    [Theory]
    [InlineData("https://spsprodwus24.vssps.visualstudio.com/_signin?realm=dev.azure.com&redirect=1", "https://spsprodwus24.vssps.visualstudio.com/_signin")]
    [InlineData("/_signin?realm=dev.azure.com", "https://dev.azure.com/_signin")]
    public async Task GetProjectsAsync_RedirectToSignIn_ThrowsUnauthorizedNamingTheTarget(string location, string target)
    {
        _handler.EnqueueResponse(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            return response;
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetProjectsAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Contains($"at {target} with 302", ex.Message);
    }

    [Fact]
    public async Task GetProjectsAsync_NonAuthoritativeJson_IsAccepted()
    {
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.NonAuthoritativeInformation)
        {
            Content = new StringContent("""{"count":1,"value":[{"id":"p1","name":"Project"}]}""", Encoding.UTF8, "application/json")
        });

        var projects = await _client.GetProjectsAsync();

        Assert.Equal("Project", Assert.Single(projects).Name);
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData(null)]
    public async Task GetProjectsAsync_NonAuthoritativeWithoutJson_ThrowsUnauthorized(string? mediaType)
    {
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.NonAuthoritativeInformation)
        {
            Content = mediaType is null
                ? new ByteArrayContent([])
                : new StringContent("Sign in", Encoding.UTF8, mediaType)
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetProjectsAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task GetProjectsAsync_JsonOnOk_IsAccepted()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":1,"value":[{"id":"p1","name":"Project"}]}""");

        var projects = await _client.GetProjectsAsync();

        Assert.Equal("Project", Assert.Single(projects).Name);
    }

    [Fact]
    public async Task GetProjectsAsync_NoContent_ThrowsInsteadOfReadingAsNoProjects()
    {
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.NoContent));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetProjectsAsync());

        Assert.Equal(HttpStatusCode.NoContent, ex.StatusCode);
    }

    [Fact]
    public async Task GetWorkItemByIdAsync_NoContent_ThrowsInsteadOfReadingAsNotFound()
    {
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.NoContent));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetWorkItemByIdAsync(1));

        Assert.Equal(HttpStatusCode.NoContent, ex.StatusCode);
    }

    [Fact]
    public async Task GetWorkItemsByQueryAsync_NoContentOnWiql_ThrowsInsteadOfReadingAsNoMatches()
    {
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.NoContent));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => _client.GetWorkItemsByQueryAsync("SELECT [System.Id] FROM WorkItems"));

        Assert.Equal(HttpStatusCode.NoContent, ex.StatusCode);
    }

    [Fact]
    public async Task GetProjectsAsync_NoContentLabelledAsJson_ThrowsInsteadOfReadingAsNoProjects()
    {
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.NoContent)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetProjectsAsync());

        Assert.Equal(HttpStatusCode.NoContent, ex.StatusCode);
    }

    [Fact]
    public async Task GetProjectsAsync_EmptyOkWithoutContentType_ThrowsWithItsOwnStatus()
    {
        _handler.EnqueueResponse(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetProjectsAsync());

        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
    }

    private static HttpResponseMessage SignInPage(HttpStatusCode status) => new(status)
    {
        Content = new StringContent("<html><body>Sign in to your account</body></html>", Encoding.UTF8, "text/html")
    };
}
