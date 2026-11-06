using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Resilience;
using Jezda.Common.Integrations.AzureDevOps.Configuration;
using Jezda.Common.Integrations.AzureDevOps.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;

namespace Jezda.Common.Integrations.AzureDevOps.Extensions;

public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddAzureDevOpsIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AzureDevOpsOptions>(configuration.GetSection(AzureDevOpsOptions.SectionName));

        // Existing typed HttpClient for IAzureDevOpsClient
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

            // Without it, a bad PAT is redirected to a sign-in page that ends as 203 with HTML,
            // which IsSuccessStatusCode accepts. With it, Azure DevOps answers 401.
            client.DefaultRequestHeaders.Add("X-TFS-FedAuthRedirect", "Suppress");
        });

        // Named HttpClient for IExternalTaskProvider (per-request auth + base URL)
        services.AddHttpClient(AzureDevOpsTaskProvider.HttpClientName, client =>
        {
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).AddIntegrationResilience();

        services.AddSingleton<IExternalTaskProvider, AzureDevOpsTaskProvider>();

        return services;
    }
}
