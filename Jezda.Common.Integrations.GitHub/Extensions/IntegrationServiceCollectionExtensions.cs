using System.Net.Http.Headers;
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

        services.AddHttpClient<IGitHubClient, GitHubClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<GitHubOptions>>().Value;

            client.BaseAddress = new Uri(options.BaseUrl);

            if (!string.IsNullOrEmpty(options.AccessToken))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
            }

            client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
        });

        return services;
    }
}
