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
| `GetProjectsAsync` | `boards(limit: 100, page: $page)` | Returns boards as projects, every page |
| `GetTasksAsync` | `boards(ids: $board) { items_page(limit: 500) }`, then `next_items_page(cursor: $cursor)` | Returns items as tasks, every page |
| `SearchTasksAsync` | the two above, per board | Fan-out search; skips a board that answers 404 or 403 |

Ids and cursors go in GraphQL `variables`, never into the query text. `GetTasksAsync` throws
`ArgumentException` for a board id that is not all digits, without a request.

## Connection validation

`ValidateConnectionAsync` returns a rejection when Monday.com rejects the token: 401, or one of
`NOT_AUTHENTICATED`, `Unauthorized`, `UserUnauthorizedException` (all `InvalidToken`), `USER_ACCESS_DENIED`
(`AccessDenied`) or `missingRequiredPermissions` (`MissingScope`) as a GraphQL error on status 200 or 403. An IP restriction, a temporary API block,
a 403 without one of those codes and every other failure propagate, so they are not shown to the user as a bad
token.

A token with a character outside printable ASCII, such as a pasted smart quote or line break, returns
`NotSendable`, without a request: it cannot go in a header, and the failure would otherwise read as an outage.
The other calls throw `ArgumentException` for such a token, also without a request, so it is not
retried and does not count towards the circuit breaker.

`IExternalTaskProvider` keeps one circuit breaker per token: every caller is on `api.monday.com`, so
one caller whose requests keep failing does not cut off the others. The breaker key holds a hash of
the token, never the token.

## Failures on status 200

Monday.com reports application errors with status 200. `GetProjectsAsync` and `GetTasksAsync` throw
`HttpRequestException` when the answer has any GraphQL error, or has no data at all, so a failure does not read
as "no boards" or "no tasks". That includes partial data next to an error: the list may have gaps, and callers
read it as complete. The message names the error code, or the error message where there is no code.

The exception says what went wrong without the caller reading its message:

| Answer | Exception |
|--------|-----------|
| A rate limit, complexity budget, concurrency or daily limit, `API_TEMPORARILY_BLOCKED`, or status 429 | `IntegrationRateLimitException` (429), `RetryAt` from `retry_in_seconds` or `Retry-After` |
| `Unauthorized`, `NOT_AUTHENTICATED` | `HttpRequestException` with 401 |
| `UserUnauthorizedException`, `USER_ACCESS_DENIED`, `missingRequiredPermissions` | 403, or the `status_code` Monday.com sends |
| `InvalidBoardIdException`, `ResourceNotFoundException`, or a board the answer leaves out | 404 |
| Any other error | the `status_code` Monday.com sends, else the response status |
| A body that is not JSON | the response status; on 200 the message says a proxy or login page likely answered |

Error codes are read from the body on every status, so a 401 or 429 keeps its code.
