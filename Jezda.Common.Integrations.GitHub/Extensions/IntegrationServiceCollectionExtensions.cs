using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jezda.Common.Integrations.GitHub.Extensions;

public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddGitHubIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GitHubOptions>(
            configuration.GetSection(GitHubOptions.SectionName));

        services.AddHttpClient<IGitHubClient, GitHubClient>();

        return services;
    }
}
