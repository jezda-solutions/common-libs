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

`ValidateConnectionAsync` returns `false` only when Monday.com rejects the token: 401 or 403, or one of
`NOT_AUTHENTICATED`, `Unauthorized`, `UserUnauthorizedException`, `USER_ACCESS_DENIED`,
`missingRequiredPermissions` as a GraphQL error on status 200. An IP restriction, a temporary API block and
every other failure propagate, so they are not shown to the user as a bad token.
