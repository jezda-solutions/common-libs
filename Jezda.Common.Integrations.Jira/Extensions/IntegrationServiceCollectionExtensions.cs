using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jezda.Common.Integrations.Jira.Extensions;

public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddJiraIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JiraOptions>(
            configuration.GetSection(JiraOptions.SectionName));

        services.AddHttpClient<IJiraClient, JiraClient>();

        return services;
    }
}
