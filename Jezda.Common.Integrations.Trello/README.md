# Jezda.Common.Integrations.Trello

Trello integration for the Jezda Common Libraries. Provides an `IExternalTaskProvider` implementation that maps Trello boards to projects and cards to tasks.

## Authentication

Trello uses API Key + Token authentication, sent in the header `Authorization: OAuth oauth_consumer_key="<key>", oauth_token="<token>"`, never in the URL, where proxy and request logs would keep them. Format the access token using the helper:

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

## Connection validation

`ValidateConnectionAsync` returns a rejection, `InvalidToken`, only for 401, which Trello answers a bad key or token with. Every
other failure propagates, so an outage or a rate limit is not shown to the user as a bad token.

`IExternalTaskProvider` keeps one circuit breaker per token: every caller is on `api.trello.com`, so
one caller whose requests keep failing does not cut off the others. The breaker key holds a hash of
the `Authorization` header, never the key or token.

A key or token with a character that cannot go in the header (outside printable ASCII, a quote mark or a
backslash) returns `NotSendable` from `ValidateConnectionAsync` and throws `ArgumentException` from the other
calls, without a request.
