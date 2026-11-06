using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Enums;
using Jezda.Common.Integrations.AzureDevOps;
using Jezda.Common.Integrations.AzureDevOps.Extensions;
using Jezda.Common.Integrations.AzureDevOps.Providers;
using Jezda.Common.Integrations.ClickUp.Extensions;
using Jezda.Common.Integrations.GitHub;
using Jezda.Common.Integrations.GitHub.Extensions;
using Jezda.Common.Integrations.Jira;
using Jezda.Common.Integrations.Jira.Extensions;
using Jezda.Common.Integrations.Monday.Extensions;
using Jezda.Common.Integrations.Trello.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    [Theory]
    [InlineData(nameof(IAzureDevOpsClient))]
    [InlineData(AzureDevOpsTaskProvider.HttpClientName)]
    public void AddAzureDevOpsIntegration_ClientsAskNotToRedirectToSignIn(string clientName)
    {
        var services = new ServiceCollection();
        services.AddAzureDevOpsIntegration(CreateConfiguration());

        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);

        // Without it a bad PAT gets a sign-in page ending as 203, instead of a plain 401.
        Assert.Equal(["Suppress"], client.DefaultRequestHeaders.GetValues("X-TFS-FedAuthRedirect"));
    }

    [Theory]
    [InlineData(nameof(IAzureDevOpsClient))]
    [InlineData(AzureDevOpsTaskProvider.HttpClientName)]
    public void AddAzureDevOpsIntegration_UnknownPrimaryHandler_WarnsThatRedirectsStayOn(string clientName)
    {
        var logs = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerProvider>(logs);
        services.ConfigureHttpClientDefaults(builder =>
            builder.ConfigurePrimaryHttpMessageHandler(() => new CustomPrimaryHandler()));
        services.AddAzureDevOpsIntegration(CreateConfiguration());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(clientName);

        var warning = Assert.Single(logs.Warnings);
        Assert.Contains(nameof(CustomPrimaryHandler), warning);
    }

    [Theory]
    [InlineData(nameof(IAzureDevOpsClient))]
    [InlineData(AzureDevOpsTaskProvider.HttpClientName)]
    public void AddAzureDevOpsIntegration_DefaultPrimaryHandler_DoesNotWarn(string clientName)
    {
        var logs = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerProvider>(logs);
        services.AddAzureDevOpsIntegration(CreateConfiguration());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(clientName);

        Assert.Empty(logs.Warnings);
    }

    private sealed class CustomPrimaryHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider, ILogger
    {
        private readonly List<string> _warnings = [];

        public IReadOnlyList<string> Warnings
        {
            get
            {
                lock (_warnings)
                {
                    return [.. _warnings];
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                lock (_warnings)
                {
                    _warnings.Add(formatter(state, exception));
                }
            }
        }

        public void Dispose()
        {
        }
    }

    [Theory]
    [InlineData(nameof(IAzureDevOpsClient))]
    [InlineData(AzureDevOpsTaskProvider.HttpClientName)]
    public void AddAzureDevOpsIntegration_ClientsDoNotFollowRedirects(string clientName)
    {
        var services = new ServiceCollection();
        services.AddAzureDevOpsIntegration(CreateConfiguration());

        using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(clientName);

        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler!;
        }

        // .NET drops Authorization on a followed redirect, so a valid PAT would answer 401.
        var allowAutoRedirect = handler switch
        {
            SocketsHttpHandler sockets => sockets.AllowAutoRedirect,
            HttpClientHandler client => client.AllowAutoRedirect,
            _ => throw new InvalidOperationException($"Unexpected primary handler {handler.GetType()}")
        };
        Assert.False(allowAutoRedirect);
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
    public void AddTrelloIntegration_RegistersTaskProvider()
    {
        var services = new ServiceCollection();

        services.AddTrelloIntegration();

        var provider = services.BuildServiceProvider();

        var taskProviders = provider.GetServices<IExternalTaskProvider>().ToList();
        Assert.Single(taskProviders);
        Assert.Equal(ExternalProvider.Trello, taskProviders[0].Provider);
    }

    [Fact]
    public void AddClickUpIntegration_RegistersTaskProvider()
    {
        var services = new ServiceCollection();

        services.AddClickUpIntegration();

        var provider = services.BuildServiceProvider();

        var taskProviders = provider.GetServices<IExternalTaskProvider>().ToList();
        Assert.Single(taskProviders);
        Assert.Equal(ExternalProvider.ClickUp, taskProviders[0].Provider);
    }

    [Fact]
    public void AddMondayIntegration_RegistersTaskProvider()
    {
        var services = new ServiceCollection();

        services.AddMondayIntegration();

        var provider = services.BuildServiceProvider();

        var taskProviders = provider.GetServices<IExternalTaskProvider>().ToList();
        Assert.Single(taskProviders);
        Assert.Equal(ExternalProvider.Monday, taskProviders[0].Provider);
    }

    [Fact]
    public void AllIntegrations_RegistersSixTaskProviders()
    {
        var services = new ServiceCollection();
        var config = CreateConfiguration();

        services.AddGitHubIntegration(config);
        services.AddJiraIntegration(config);
        services.AddAzureDevOpsIntegration(config);
        services.AddTrelloIntegration();
        services.AddClickUpIntegration();
        services.AddMondayIntegration();

        var provider = services.BuildServiceProvider();

        var taskProviders = provider.GetServices<IExternalTaskProvider>().ToList();
        Assert.Equal(6, taskProviders.Count);
        Assert.Contains(taskProviders, p => p.Provider == ExternalProvider.GitHub);
        Assert.Contains(taskProviders, p => p.Provider == ExternalProvider.Jira);
        Assert.Contains(taskProviders, p => p.Provider == ExternalProvider.AzureDevOps);
        Assert.Contains(taskProviders, p => p.Provider == ExternalProvider.Trello);
        Assert.Contains(taskProviders, p => p.Provider == ExternalProvider.ClickUp);
        Assert.Contains(taskProviders, p => p.Provider == ExternalProvider.Monday);
    }
}
