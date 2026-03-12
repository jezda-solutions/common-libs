using System.Net.Http.Headers;
using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Resilience;
using Jezda.Common.Integrations.Monday.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Jezda.Common.Integrations.Monday.Extensions;

public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddMondayIntegration(
        this IServiceCollection services)
    {
        services.AddHttpClient(MondayTaskProvider.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.monday.com/v2");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).AddIntegrationResilience();

        services.AddSingleton<IExternalTaskProvider, MondayTaskProvider>();

        return services;
    }
}
