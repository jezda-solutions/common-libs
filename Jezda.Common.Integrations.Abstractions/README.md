# Jezda.Common.Integrations.Abstractions

Shared abstractions for external task provider integrations, including the `IExternalTaskProvider` interface, DTOs, and resilience defaults.

## Key Types

- **`IExternalTaskProvider`** — unified interface for importing tasks from external providers (GitHub, Jira, Azure DevOps)
- **`ExternalProvider`** — enum identifying the provider
- **`ExternalProjectDto`** / **`ExternalTaskDto`** — normalized DTOs returned by all providers
- **`HttpResilienceDefaults.AddIntegrationResilience()`** — extension method for configuring standard resilience policies on `IHttpClientBuilder`

## Usage

Implementations are provided by the provider-specific packages:
- `Jezda.Common.Integrations.GitHub`
- `Jezda.Common.Integrations.Jira`
- `Jezda.Common.Integrations.AzureDevOps`

Resolve all registered providers via `IEnumerable<IExternalTaskProvider>` and filter by the `Provider` property.
