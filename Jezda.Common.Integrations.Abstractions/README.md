# Jezda.Common.Integrations.Abstractions

Shared abstractions for external task provider integrations, including the `IExternalTaskProvider` interface, DTOs, and resilience defaults.

## Key Types

- **`IExternalTaskProvider`** — unified interface for importing tasks from external providers (GitHub, Jira, Azure DevOps)
- **`ExternalProvider`** — enum identifying the provider
- **`ExternalProjectDto`** / **`ExternalTaskDto`** — normalized DTOs returned by all providers
- **`ConnectionValidationResult`** — what `ValidateConnectionAsync` returns: `IsValid`, and for a rejected token a `TokenRejectionReason` (`InvalidToken`, `NotSendable`, `MissingScope`, `AccessDenied`, `SignInPage`) plus a `Detail` for logs and support (status and path, or where a sign-in page is; never the token or the response body). Map `Reason` to a message for the user; `Detail` is English and not meant for display
- **`HttpResilienceDefaults.AddIntegrationResilience()`** — extension method for configuring standard resilience policies on `IHttpClientBuilder`. It retries a 429 only when `Retry-After` asks for at most 5 s; a longer pause, or a 429 without `Retry-After`, reaches the caller at once instead of ending as a timeout. Each address (scheme, host and port) has its own circuit breaker, so one customer's site that is down does not cut off the others; credentials typed into a base URL never reach the key. The overload `AddIntegrationResilience(pipelineKey)` takes another key for providers whose callers share one address: Azure DevOps adds the organisation on `dev.azure.com`, GitHub, Monday and ClickUp use `IntegrationPipelineKeys.AddressAndAuthorization` (address plus a hash of the `Authorization` header), and Trello hashes its `token` query parameter the same way. A 429 does not count towards the breaker, which tokens on the same key share, so one token's rate limit does not cut off the others; only when it answers the single probe of a half-open breaker does it keep the breaker open, since it says nothing about whether the provider recovered
- **`IntegrationRateLimitException`** — an `HttpRequestException` with status 429 that a provider throws when it recognises a rate limit, even one answered with 403; `RetryAt` says when to try again, when the provider said. GitHub throws it for now
- **`HttpRedirectDefaults.DisableAutoRedirect()`** — extension method that stops an `IHttpClientBuilder` from following redirects, which would drop the `Authorization` header and make a valid token read as expired

## Connection validation

**Breaking change:** `ValidateConnectionAsync` returns `ConnectionValidationResult` instead of `bool`, so a caller can tell a revoked token from one without a scope. Replace `if (await provider.ValidateConnectionAsync(...))` with `.IsValid`, and show the user a message per `Reason`:

| `Reason` | What the user should do |
|---|---|
| `InvalidToken` | Create a new token; this one is wrong, revoked or expired |
| `NotSendable` | Paste the token again without stray characters (a smart quote, a line break) |
| `MissingScope` | Add the missing scope to the token (Azure DevOps: Work Items (Read); Monday.com: the permission behind `missingRequiredPermissions`); a new token with the same scopes fails the same way |
| `AccessDenied` | Check the token's scopes and any Conditional Access, IP allow list or firewall in front of the provider |
| `SignInPage` | Check the base URL and the token; a gateway may ask for its own sign-in (`Detail` names where) |

Exceptions keep their meaning: "could not check", never "bad token".

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
