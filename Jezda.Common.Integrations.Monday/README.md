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
