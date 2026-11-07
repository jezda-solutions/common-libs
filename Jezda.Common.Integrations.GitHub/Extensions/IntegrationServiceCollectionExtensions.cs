using System.Net.Http.Headers;
using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Resilience;
using Jezda.Common.Integrations.GitHub.Providers;
using Jezda.Common.Integrations.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jezda.Common.Integrations.GitHub.Extensions;

public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddGitHubIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GitHubOptions>(
            configuration.GetSection(GitHubOptions.SectionName));

        // Existing typed HttpClient for IGitHubClient
        services.AddHttpClient<IGitHubClient, GitHubClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<GitHubOptions>>().Value;

            client.BaseAddress = IntegrationBaseAddress.Create(options.BaseUrl);

            if (!string.IsNullOrEmpty(options.AccessToken))
            {
                // A bad value in configuration would otherwise fail every request as an outage.
                if (!AccessTokenRules.CanGoInHeader(options.AccessToken))
                {
                    throw new InvalidOperationException(
                        "GitHub AccessToken has characters that cannot go in a request header, such as a pasted smart quote or line break.");
                }

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
            }

            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        });

        // Named HttpClient for IExternalTaskProvider (per-request auth)
        services.AddHttpClient(GitHubTaskProvider.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Jezda-Common-Integration-Client");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        }).AddIntegrationResilience();

        services.AddSingleton<IExternalTaskProvider, GitHubTaskProvider>();

        return services;
    }
}
