# Jezda.Common.Integrations.Monday

Monday.com integration for the Jezda Common Libraries. Provides an `IExternalTaskProvider` implementation that maps Monday.com boards to projects and items to tasks via the GraphQL API.

## Authentication

Monday.com uses a V2 API token passed as a Bearer token.

## Registration

```csharp
services.AddMondayIntegration();
```

## API Mapping

| Method | Monday.com GraphQL | Description |
|--------|-------------------|-------------|
| `ValidateConnectionAsync` | `{ me { id } }` | Validates API token |
| `GetProjectsAsync` | `{ boards(limit:100) { ... } }` | Returns boards as projects |
| `GetTasksAsync` | `{ boards(ids:N) { items_page { items { ... } } } }` | Returns items as tasks |

## Connection validation

`ValidateConnectionAsync` returns a rejection when Monday.com rejects the token: 401, or one of
`NOT_AUTHENTICATED`, `Unauthorized`, `UserUnauthorizedException` (all `InvalidToken`), `USER_ACCESS_DENIED`
(`AccessDenied`) or `missingRequiredPermissions` (`MissingScope`) as a GraphQL error on status 200 or 403. An IP restriction, a temporary API block,
a 403 without one of those codes and every other failure propagate, so they are not shown to the user as a bad
token.

A token with a character outside printable ASCII, such as a pasted smart quote or line break, returns
`NotSendable`, without a request: it cannot go in a header, and the failure would otherwise read as an outage.
The other calls throw `ArgumentException` for such a token, also without a request, so it is not
retried and does not count towards the circuit breaker every caller of the provider shares.

## Failures on status 200

Monday.com reports application errors with status 200. `GetProjectsAsync` and `GetTasksAsync` throw
`HttpRequestException` when the answer has any GraphQL error, or has no data at all, so a failure does not read
as "no boards" or "no tasks". That includes partial data next to an error: the list may have gaps, and callers
read it as complete. The message names the error code, or the error message where there is no code.
