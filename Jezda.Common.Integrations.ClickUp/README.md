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
