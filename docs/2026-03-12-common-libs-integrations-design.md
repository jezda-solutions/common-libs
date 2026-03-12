# Phase 2: common-libs Integration Clients

**Date:** 2026-03-12
**Status:** Pending
**Module:** `common-libs/Jezda.Common.Integrations.*` (separate repository)
**Depends on:** Phase 1 (schema readiness)

## Overview

Create `Jezda.Common.Integrations.*` NuGet packages implementing `IExternalTaskProvider` for GitHub, Jira, and Azure DevOps. These are consumed by TMS in Phase 3.

## Provider Interface

```csharp
// Jezda.Common.Integrations.Abstractions
public interface IExternalTaskProvider
{
    ExternalProvider Provider { get; }
    Task<bool> ValidateConnectionAsync(string accessToken, string? baseUrl, CancellationToken ct);
    Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(string accessToken, string? baseUrl, CancellationToken ct);
    Task<IReadOnlyList<ExternalTaskDto>> GetTasksAsync(string accessToken, string? baseUrl, string projectId, CancellationToken ct);
}
```

## Packages

### Jezda.Common.Integrations.Abstractions
- `IExternalTaskProvider` interface
- DTOs: `ExternalProjectDto`, `ExternalTaskDto`
- Shared `HttpResiliencePolicies` (Polly retry + circuit breaker)
- `ExternalProvider` enum (shared with TMS Domain)

### Jezda.Common.Integrations.GitHub
- `GitHubTaskProvider : IExternalTaskProvider`
- Named HttpClient `"GitHubHttpClient"` via `IHttpClientFactory`
- GitHub REST API v3 (repos → issues)
- `services.AddGitHubIntegration()` extension

### Jezda.Common.Integrations.Jira
- `JiraTaskProvider : IExternalTaskProvider`
- Named HttpClient `"JiraHttpClient"` via `IHttpClientFactory`
- Jira REST API v3 (projects → issues)
- `services.AddJiraIntegration()` extension

### Jezda.Common.Integrations.AzureDevOps
- `AzureDevOpsTaskProvider : IExternalTaskProvider`
- Named HttpClient `"AzureDevOpsHttpClient"` via `IHttpClientFactory`
- ADO REST API (projects → work items)
- `services.AddAzureDevOpsIntegration()` extension

## HttpClientFactory Pattern

Each provider registers a named HttpClient with:
- Base address and default headers
- `SocketsHttpHandler` for connection pooling
- Polly retry + circuit breaker policies
- Per-request `Authorization` header injection (user PATs, not baked into client config)

```csharp
public class GitHubTaskProvider : IExternalTaskProvider
{
    private readonly IHttpClientFactory _httpClientFactory;

    public async Task<IReadOnlyList<ExternalProjectDto>> GetProjectsAsync(
        string accessToken, string? baseUrl, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("GitHubHttpClient");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        // ... API calls
    }
}
```

## Testing

- Unit tests per provider with `MockHttpMessageHandler`
- Validate connection with invalid PAT returns `false`
- Deserialize real API response shapes

## Estimated Scope

~20 new files across 4 NuGet packages.
