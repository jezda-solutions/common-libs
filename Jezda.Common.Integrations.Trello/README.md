# Jezda.Common.Integrations.Trello

Trello integration for the Jezda Common Libraries. Provides an `IExternalTaskProvider` implementation that maps Trello boards to projects and cards to tasks.

## Authentication

Trello uses API Key + Token authentication via query parameters. Format the access token using the helper:

```csharp
var accessToken = TrelloTaskProvider.FormatAccessToken("your-api-key", "your-api-token");
```

## Registration

```csharp
services.AddTrelloIntegration();
```

## API Mapping

| Method | Trello API | Description |
|--------|-----------|-------------|
| `ValidateConnectionAsync` | `GET /1/members/me` | Validates API key and token |
| `GetProjectsAsync` | `GET /1/members/me/boards` | Returns boards as projects |
| `GetTasksAsync` | `GET /1/boards/{id}/cards` | Returns cards as tasks |
