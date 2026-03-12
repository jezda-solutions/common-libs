using System.Net.Http.Headers;
using Jezda.Common.Integrations.Abstractions;
using Jezda.Common.Integrations.Abstractions.Resilience;
using Jezda.Common.Integrations.Trello.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Jezda.Common.Integrations.Trello.Extensions;

public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddTrelloIntegration(
        this IServiceCollection services)
    {
        services.AddHttpClient(TrelloTaskProvider.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.trello.com/1/");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).AddIntegrationResilience();

        services.AddSingleton<IExternalTaskProvider, TrelloTaskProvider>();

        return services;
    }
}
