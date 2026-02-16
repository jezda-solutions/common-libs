using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jezda.Common.Integrations.Jira.Extensions;

public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddJiraIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JiraOptions>(
            configuration.GetSection(JiraOptions.SectionName));

        services.AddHttpClient<IJiraClient, JiraClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<JiraOptions>>().Value;

            if (string.IsNullOrEmpty(options.BaseUrl))
            {
                throw new InvalidOperationException("Jira BaseUrl is not configured.");
            }

            client.BaseAddress = new Uri(options.BaseUrl);

            if (!string.IsNullOrEmpty(options.Email) && !string.IsNullOrEmpty(options.ApiToken))
            {
                var authString = $"{options.Email}:{options.ApiToken}";
                var base64Auth = Convert.ToBase64String(Encoding.ASCII.GetBytes(authString));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Auth);
            }

            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        return services;
    }
}
