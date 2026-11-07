# Jezda.Common.Integrations.AzureDevOps

A strongly-typed .NET client for Azure DevOps REST API, built on top of `Jezda.Common.Integrations`. 
This library simplifies interaction with Azure DevOps by providing easy-to-use methods for Work Items and WIQL queries.

## ✨ Features

- **Work Item Management**: Create and retrieve work items.
- **WIQL Support**: Execute raw WIQL queries and get typed results.
- **Project Management**: List all projects in the organization.
- **Dependency Injection**: Ready-to-use `IServiceCollection` extension.

## 📦 Installation

Add the project reference or NuGet package:

```bash
dotnet add package Jezda.Common.Integrations.AzureDevOps
```

## ⚙️ Configuration

Add the following section to your `appsettings.json`:

```json
{
  "Integrations": {
    "AzureDevOps": {
      "BaseUrl": "https://dev.azure.com/your-organization/",
      "PersonalAccessToken": "your-pat-token",
      "ApiVersion": "7.1"
    }
  }
}
```

## 🚀 Usage

### 1. Register Services
In your `Program.cs`:

```csharp
using Jezda.Common.Integrations.Extensions;

builder.Services.AddAzureDevOpsIntegration(builder.Configuration);
```

### 2. Inject and Use
Inject `IAzureDevOpsClient` into your service:

```csharp
public class MyService
{
    private readonly IAzureDevOpsClient _adoClient;

    public MyService(IAzureDevOpsClient adoClient)
    {
        _adoClient = adoClient;
    }

    public async Task SyncDataAsync()
    {
        // 1. Get all projects
        var projects = await _adoClient.GetProjectsAsync();

        // 2. Get work items via WIQL
        var workItems = await _adoClient.GetWorkItemsByQueryAsync(
            "SELECT [System.Id] FROM WorkItems WHERE [System.State] = 'Active'");
    }
}
```

`GetWorkItemsByQueryAsync` takes flat queries only. A link query (`FROM WorkItemLinks`) throws
`NotSupportedException`. The result can hold fewer items than the query matched: an item deleted or
made unreadable after the query ran is left out, with a warning in the log.

## 🔐 Errors and authentication

Both `IAzureDevOpsClient` and the `IExternalTaskProvider` for Azure DevOps report failures as
`HttpRequestException` with `StatusCode` set, so a caller can tell a rejected token from anything else:

| What happened | Result |
|---|---|
| Token rejected (401, 403) | `StatusCode` 401 / 403 |
| Sign-in page instead of JSON (a redirect to `/_signin`, or 203 without JSON) | `StatusCode` 401; for a redirect, the message names the target's host and path. |
| A body that is not JSON on any other success status (the base URL points at a website, a proxy or a captive portal answered). Token validation treats 204 the same way. | `StatusCode` is the success status itself, e.g. 200. The token was not checked, so this is not 401. |
| Base URL redirects anywhere else (3xx, e.g. after an organisation rename) | `StatusCode` is the 3xx; the message names the target. Redirects are not followed, because .NET drops `Authorization` on the next hop and a valid PAT would read as expired. |
| Any other failure | Its own status, or the original exception for dropped connections and cancellation. A timeout on `IAzureDevOpsClient` is `TaskCanceledException` from `HttpClient.Timeout`. |
| Resilience pipeline on the `IExternalTaskProvider` client gave up | Polly's `TimeoutRejectedException`, `BrokenCircuitException` or `RateLimiterRejectedException`. None derives from `HttpRequestException`, so also catch their common base, `ExecutionRejectedException`. |

`ValidateConnectionAsync` returns `false` only for a rejected token (401, 403, sign-in page). Every other
failure propagates, so an outage is not shown to the user as a bad PAT.

A 403 is not always about the token: Conditional Access, an IP allow-list or a WAF can answer it for a valid
PAT. Validation still returns `false`, and logs a warning with the start of the response body, which tells these
apart.

Validation sends two requests: `_apis/projects`, which needs "Project and Team (Read)", and `_apis/wit/fields`,
which needs "Work Items (Read)". A PAT missing either scope returns `false`, instead of validating and failing
later on the first work item call.

### Keeping redirects off

`AddAzureDevOpsIntegration` turns off redirects on the default primary handler. Without that, a redirect drops
`Authorization` and a valid PAT reads as expired. It also sets the `X-TFS-FedAuthRedirect: Suppress` header, which
asks for 401 instead of a redirect to sign-in. Four setups need care:

- **A custom primary handler registered before `AddAzureDevOpsIntegration`:** one that is not `SocketsHttpHandler`
  or `HttpClientHandler` stays as it is, and a warning is logged. Make sure it does not follow redirects.
- **`ConfigurePrimaryHttpMessageHandler` called after `AddAzureDevOpsIntegration`:** the handler replaces the
  configured one and nothing warns, so set `AllowAutoRedirect = false` on it yourself.
- **`AzureDevOpsClient` given an `HttpClient` made outside DI:** set `AllowAutoRedirect = false` on its handler,
  and send the header yourself.
- **`AzureDevOpsTaskProvider` built on your own `IHttpClientFactory`:** the same two apply to the client it creates
  under `AzureDevOpsTaskProvider.HttpClientName`.

## 🛠️ Key Models

- **`AdoWorkItem`**: Represents a simplified work item with generic `Fields` dictionary.
- **`AdoProject`**: Contains project details (Id, Name, Description, Url).
