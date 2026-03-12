using System.Net.Http.Headers;
using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Resilience;
using Jezda.Common.Integrations.ClickUp.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Jezda.Common.Integrations.ClickUp.Extensions;

public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddClickUpIntegration(
        this IServiceCollection services)
    {
        services.AddHttpClient(ClickUpTaskProvider.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.clickup.com/api/v2/");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).AddIntegrationResilience();

        services.AddSingleton<IExternalTaskProvider, ClickUpTaskProvider>();

        return services;
    }
}
