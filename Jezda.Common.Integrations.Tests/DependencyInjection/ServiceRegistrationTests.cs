using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.AzureDevOps;
using Jezda.Common.Integrations.AzureDevOps.Extensions;
using Jezda.Common.Integrations.GitHub;
using Jezda.Common.Integrations.GitHub.Extensions;
using Jezda.Common.Integrations.Jira;
using Jezda.Common.Integrations.Jira.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jezda.Common.Integrations.Tests.DependencyInjection;

public class ServiceRegistrationTests
{
    private static IConfiguration CreateConfiguration()
    {
        var builder = new ConfigurationBuilder();
        builder.AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("""
        {
            "Integrations": {
                "GitHub": { "BaseUrl": "https://api.github.com/", "AccessToken": "test-token" },
                "Jira": { "BaseUrl": "https://test.atlassian.net/", "Email": "test@example.com", "ApiToken": "jira-token" },
                "AzureDevOps": { "BaseUrl": "https://dev.azure.com/testorg/", "PersonalAccessToken": "ado-pat" }
            }
        }
        """)));
        return builder.Build();
    }

    [Fact]
    public void AddGitHubIntegration_RegistersBothClientAndTaskProvider()
    {
        var services = new ServiceCollection();
        var config = CreateConfiguration();

        services.AddGitHubIntegration(config);

        var provider = services.BuildServiceProvider();

        var typedClient = provider.GetService<IGitHubClient>();
        Assert.NotNull(typedClient);

        var taskProviders = provider.GetServices<IExternalTaskProvider>().ToList();
        Assert.Single(taskProviders);
        Assert.Equal(ExternalProvider.GitHub, taskProviders[0].Provider);
    }

    [Fact]
    public void AddJiraIntegration_RegistersBothClientAndTaskProvider()
    {
        var services = new ServiceCollection();
        var config = CreateConfiguration();

        services.AddJiraIntegration(config);

        var provider = services.BuildServiceProvider();

        var typedClient = provider.GetService<IJiraClient>();
        Assert.NotNull(typedClient);

        var taskProviders = provider.GetServices<IExternalTaskProvider>().ToList();
        Assert.Single(taskProviders);
        Assert.Equal(ExternalProvider.Jira, taskProviders[0].Provider);
    }

    [Fact]
    public void AddAzureDevOpsIntegration_RegistersBothClientAndTaskProvider()
    {
        var services = new ServiceCollection();
        var config = CreateConfiguration();

        services.AddAzureDevOpsIntegration(config);

        var provider = services.BuildServiceProvider();

        var typedClient = provider.GetService<IAzureDevOpsClient>();
        Assert.NotNull(typedClient);

        var taskProviders = provider.GetServices<IExternalTaskProvider>().ToList();
        Assert.Single(taskProviders);
        Assert.Equal(ExternalProvider.AzureDevOps, taskProviders[0].Provider);
    }

    [Fact]
    public void AllIntegrations_RegistersThreeTaskProviders()
    {
        var services = new ServiceCollection();
        var config = CreateConfiguration();

        services.AddGitHubIntegration(config);
        services.AddJiraIntegration(config);
        services.AddAzureDevOpsIntegration(config);

        var provider = services.BuildServiceProvider();

        var taskProviders = provider.GetServices<IExternalTaskProvider>().ToList();
        Assert.Equal(3, taskProviders.Count);
        Assert.Contains(taskProviders, p => p.Provider == ExternalProvider.GitHub);
        Assert.Contains(taskProviders, p => p.Provider == ExternalProvider.Jira);
        Assert.Contains(taskProviders, p => p.Provider == ExternalProvider.AzureDevOps);
    }
}
