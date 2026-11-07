using System.Net;
using Jezda.Common.Integrations.AzureDevOps;
using Jezda.Common.Integrations.AzureDevOps.Configuration;
using Jezda.Common.Integrations.AzureDevOps.Extensions;
using Jezda.Common.Integrations.AzureDevOps.Providers;
using Jezda.Common.Integrations.GitHub;
using Jezda.Common.Integrations.GitHub.Extensions;
using Jezda.Common.Integrations.Http;
using Jezda.Common.Integrations.Jira;
using Jezda.Common.Integrations.Jira.Extensions;
using Jezda.Common.Integrations.Jira.Providers;
using Jezda.Common.Integrations.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jezda.Common.Integrations.Tests.Clients;

public class IntegrationBaseAddressTests
{
    private readonly MockHttpMessageHandler _handler = new();

    [Theory]
    [InlineData("https://dev.azure.com/myorg", "https://dev.azure.com/myorg/")]
    [InlineData("https://dev.azure.com/myorg/", "https://dev.azure.com/myorg/")]
    [InlineData("https://dev.azure.com/myorg?x=1", "https://dev.azure.com/myorg/")]
    [InlineData("https://dev.azure.com/myorg/?x=1#top", "https://dev.azure.com/myorg/")]
    [InlineData("https://admin:secret@dev.azure.com/myorg", "https://dev.azure.com/myorg/")]
    [InlineData("https://tfs.local:8080/tfs/Default%20Collection", "https://tfs.local:8080/tfs/Default%20Collection/")]
    [InlineData("https://dev.azure.com:443/myorg", "https://dev.azure.com/myorg/")]
    [InlineData("https://test.atlassian.net", "https://test.atlassian.net/")]
    public void Create_KeepsSchemeHostPortAndPathOnly(string baseUrl, string expected)
    {
        Assert.Equal(expected, IntegrationBaseAddress.Create(baseUrl).AbsoluteUri);
    }

    [Theory]
    [InlineData("dev.azure.com/myorg")]
    [InlineData("/myorg")]
    public void Create_RelativeUrl_Throws(string baseUrl)
    {
        Assert.Throws<UriFormatException>(() => IntegrationBaseAddress.Create(baseUrl));
    }

    [Fact]
    public async Task AzureDevOpsTaskProvider_BaseUrlWithQuery_KeepsTheOrganisationInTheRequest()
    {
        var provider = new AzureDevOpsTaskProvider(
            new MockHttpClientFactory(_handler, AzureDevOpsTaskProvider.HttpClientName),
            NullLogger<AzureDevOpsTaskProvider>.Instance,
            Options.Create(new AzureDevOpsOptions { ApiVersion = "7.1" }));
        _handler.EnqueueResponse(HttpStatusCode.OK, """{"count":1,"value":[{"id":"p1","name":"Project"}]}""");

        var project = Assert.Single(await provider.GetProjectsAsync("pat", "https://dev.azure.com/myorg?x=1"));

        Assert.StartsWith("https://dev.azure.com/myorg/_apis/projects?", _handler.SentRequests[0].RequestUri!.AbsoluteUri);
        Assert.Equal("https://dev.azure.com/myorg/Project", project.Url);
    }

    [Fact]
    public async Task JiraTaskProvider_BaseUrlWithQuery_KeepsThePathInTheRequest()
    {
        var provider = new JiraTaskProvider(
            new MockHttpClientFactory(_handler, JiraTaskProvider.HttpClientName),
            NullLogger<JiraTaskProvider>.Instance);
        _handler.EnqueueResponse(HttpStatusCode.OK, """[]""");

        await provider.GetProjectsAsync("user@example.com:token", "https://jira.local/jira?x=1");

        Assert.StartsWith("https://jira.local/jira/rest/", _handler.SentRequests[0].RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData(nameof(IAzureDevOpsClient), "AzureDevOps", "https://admin:secret@dev.azure.com/myorg?x=1", "https://dev.azure.com/myorg/")]
    [InlineData(nameof(IJiraClient), "Jira", "https://admin:secret@test.atlassian.net", "https://test.atlassian.net/")]
    [InlineData(nameof(IGitHubClient), "GitHub", "https://admin:secret@ghe.local/api/v3", "https://ghe.local/api/v3/")]
    public void TypedClientRegistration_NormalisesTheConfiguredBaseUrl(string clientName, string section, string baseUrl, string expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"Integrations:{section}:BaseUrl"] = baseUrl
        }).Build();
        var services = new ServiceCollection();
        services.AddAzureDevOpsIntegration(configuration);
        services.AddJiraIntegration(configuration);
        services.AddGitHubIntegration(configuration);

        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);

        Assert.Equal(expected, client.BaseAddress!.AbsoluteUri);
    }
}
