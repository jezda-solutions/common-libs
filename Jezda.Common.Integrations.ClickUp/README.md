# Jezda.Common.Integrations.ClickUp

ClickUp integration for the Jezda Common Libraries. Provides an `IExternalTaskProvider` implementation that maps ClickUp teams (workspaces) to projects and tasks to tasks.

## Authentication

ClickUp uses a personal API token (`pk_...`) passed as a Bearer token.

## Registration

```csharp
services.AddClickUpIntegration();
```

## API Mapping

| Method | ClickUp API | Description |
|--------|------------|-------------|
| `ValidateConnectionAsync` | `GET /team` | Validates token and workspace access |
| `GetProjectsAsync` | `GET /team` | Returns teams as projects |
| `GetTasksAsync` | `GET /team/{id}/task` | Returns tasks for a team |

## Connection validation

`ValidateConnectionAsync` returns a rejection, `InvalidToken`, for 401, which is what ClickUp answers a bad token with.
Every other failure propagates, so an outage or a rate limit is not shown to the user as a bad token.

A token with a character outside printable ASCII, such as a pasted smart quote or line break, returns
`NotSendable`, without a request: it cannot go in a header, and the failure would otherwise read as an outage.
