# Jezda.Common.Integrations.Abstractions

Shared abstractions for external task provider integrations, including the `IExternalTaskProvider` interface, DTOs, and resilience defaults.

## Key Types

- **`IExternalTaskProvider`** — unified interface for importing tasks from external providers (GitHub, Jira, Azure DevOps)
- **`ExternalProvider`** — enum identifying the provider
- **`ExternalProjectDto`** / **`ExternalTaskDto`** — normalized DTOs returned by all providers
- **`HttpResilienceDefaults.AddIntegrationResilience()`** — extension method for configuring standard resilience policies on `IHttpClientBuilder`. It retries a 429 only when `Retry-After` asks for at most 5 s; a longer pause, or a 429 without `Retry-After`, reaches the caller at once instead of ending as a timeout
- **`IntegrationRateLimitException`** — an `HttpRequestException` with status 429 that a provider throws when it recognises a rate limit, even one answered with 403; `RetryAt` says when to try again, when the provider said. GitHub throws it for now
- **`HttpRedirectDefaults.DisableAutoRedirect()`** — extension method that stops an `IHttpClientBuilder` from following redirects, which would drop the `Authorization` header and make a valid token read as expired

## Project identity

**Breaking change in meaning:** `ExternalProjectDto.Id` and `ExternalTaskDto.ProjectId` are now the provider's durable id, and they no longer hold the name a project goes by. Rows stored by an older version hold what is now `Path`.

| Provider | `Id` / `ProjectId` | `Path` | Previous `Id` |
|---|---|---|---|
| GitHub | repository id (`1009267206`) | `owner/repo` | `owner/repo` |
| Azure DevOps | project GUID | project name | project name |
| Jira | project id (`10001`) | project key (`PROJ`) | project key |
| ClickUp, Monday, Trello | same as before | equal to `Id` | unchanged |

- Key stored rows on `Id`, and bind tasks to projects by `ExternalTaskDto.ProjectId`. A rename or transfer on the provider's side does not change it.
- Treat `Path` as a value to refresh from `GetProjectsAsync`, not as a key.
- `GetTasksAsync` accepts either `Id` or the current `Path`, so stored rows keep working while they are migrated. A row stored as a path can stop working once the project is renamed: the old path can fail with an error such as 404, and such a row can no longer be matched to a project in the list. Matching a stored row against `ExternalProjectDto.Id` does not work until it is migrated, so migrate before comparing.
- On GitHub and Azure DevOps, resolving the project costs extra requests: one per `GetTasksAsync` call, and one per distinct project in a `SearchTasksAsync` result.

## Project metadata

`CreatedAt`, `UpdatedAt`, `IsArchived` and `IsPrivate` on `ExternalProjectDto` are nullable. Null means the provider does not report the value, not that the answer is no.

| Provider | `CreatedAt` | `UpdatedAt` | `IsArchived` | `IsPrivate` |
|---|---|---|---|---|
| GitHub | `created_at` | later of `pushed_at`, `updated_at` | `archived` | `private` |
| Azure DevOps | null | `lastUpdateTime` | `state` is `deleting` or `deleted` | `visibility` is not `public` |
| Jira | null | null | `archived` | null |
| Trello | null | `dateLastActivity` | `closed` | `prefs.permissionLevel` is not `public` |
| ClickUp, Monday | null | null | null | null |

Jira's `isPrivate` means the calling user cannot see the project, not that the project is closed to the public, so `IsPrivate` stays null there. Azure DevOps has no archive feature, and its project list returns only `wellFormed` projects by default, so `IsArchived` there is `false` in practice.

## Usage

Implementations are provided by the provider-specific packages:
- `Jezda.Common.Integrations.GitHub`
- `Jezda.Common.Integrations.Jira`
- `Jezda.Common.Integrations.AzureDevOps`

Resolve all registered providers via `IEnumerable<IExternalTaskProvider>` and filter by the `Provider` property.
