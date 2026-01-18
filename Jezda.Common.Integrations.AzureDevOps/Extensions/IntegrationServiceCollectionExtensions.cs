using System.Net.Http.Headers;
using System.Text;
using Jezda.Common.Integrations.AzureDevOps;
using Jezda.Common.Integrations.AzureDevOps.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Jezda.Common.Integrations.Extensions;

public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddAzureDevOpsIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AzureDevOpsOptions>(configuration.GetSection(AzureDevOpsOptions.SectionName));

        services.AddHttpClient<IAzureDevOpsClient, AzureDevOpsClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<AzureDevOpsOptions>>().Value;
            
            if (string.IsNullOrEmpty(options.BaseUrl))
            {
                throw new InvalidOperationException("Azure DevOps BaseUrl is not configured.");
            }

            client.BaseAddress = new Uri(options.BaseUrl);
            
            if (!string.IsNullOrEmpty(options.PersonalAccessToken))
            {
                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{options.PersonalAccessToken}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
            }
            
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });
        // Note: Retry policy (Polly) can be added here using .AddTransientHttpErrorPolicy(...) 
        // if Microsoft.Extensions.Http.Polly is referenced.

        return services;
    }
}
