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

    [Theory]
    [InlineData(HttpStatusCode.NonAuthoritativeInformation)]
    [InlineData(HttpStatusCode.OK)]
    public async Task GetProjectsAsync_SignInPage_ThrowsUnauthorized(HttpStatusCode status)
    {
        _handler.EnqueueResponse(() => SignInPage(status));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetProjectsAsync());

        // Not a JSON parsing error without a status: 401, which callers read as an expired token.
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
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
    [InlineData("https://spsprodwus24.vssps.visualstudio.com/_signin?realm=dev.azure.com&redirect=1")]
    [InlineData("/_signin?realm=dev.azure.com")]
    public async Task GetProjectsAsync_RedirectToSignIn_ThrowsUnauthorized(string location)
    {
        _handler.EnqueueResponse(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            return response;
        });

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetProjectsAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
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

    [Fact]
    public async Task GetProjectsAsync_JsonOnOk_IsAccepted()
    {
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":1,"value":[{"id":"p1","name":"Project"}]}""");

        var projects = await _client.GetProjectsAsync();

        Assert.Equal("Project", Assert.Single(projects).Name);
    }

    private static HttpResponseMessage SignInPage(HttpStatusCode status) => new(status)
    {
        Content = new StringContent("<html><body>Sign in to your account</body></html>", Encoding.UTF8, "text/html")
    };
}
