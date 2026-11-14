# Jezda.Common.Integrations.Jira

A strongly-typed .NET client for Jira Cloud REST API (v3), built on top of `Jezda.Common.Integrations`.

## ✨ Features

- **Projects**: List all visible projects.
- **Search**: Search issues using JQL (Jira Query Language).
- **Authentication**: Supports Basic Auth (Email + API Token) for Jira Cloud.
- **Dependency Injection**: Ready-to-use `IServiceCollection` extension.

## 📦 Installation

Add the project reference or NuGet package:

```bash
dotnet add package Jezda.Common.Integrations.Jira
```

## ⚙️ Configuration

Add the following section to your `appsettings.json`:

```json
{
  "Integrations": {
    "Jira": {
      "BaseUrl": "https://your-domain.atlassian.net/",
      "Email": "your-email@example.com",
      "ApiToken": "your-atlassian-api-token"
    }
  }
}
```

> **Note**: For Jira Cloud, you must use an API Token, not your password. Generate one at [id.atlassian.com](https://id.atlassian.com/manage-profile/security/api-tokens).

## 🚀 Usage

### 1. Register Services
In your `Program.cs`:

```csharp
using Jezda.Common.Integrations.Jira.Extensions;

builder.Services.AddJiraIntegration(builder.Configuration);
```

### 2. Inject and Use
Inject `IJiraClient` into your service:

```csharp
public class JiraService
{
    private readonly IJiraClient _jiraClient;

    public JiraService(IJiraClient jiraClient)
    {
        _jiraClient = jiraClient;
    }

    public async Task PrintHighPriorityBugsAsync()
    {
        // Search using JQL
        var jql = "project = 'MYPROJ' AND issuetype = Bug AND priority = High";
        var result = await _jiraClient.SearchIssuesAsync(jql);
        
        if (result != null)
        {
            foreach(var issue in result.Issues)
            {
                 Console.WriteLine($"[{issue.Key}] {issue.Fields?.Summary} - {issue.Fields?.Status?.Name}");
            }
        }
    }
}
```

## Connection validation

`ValidateConnectionAsync` returns a rejection, `InvalidToken`, only when Jira rejects the token: 401. With
`X-Seraph-LoginReason: AUTHENTICATION_DENIED` it throws instead, because the CAPTCHA was triggered and the
token was not checked; the user has to sign in through the Jira website. Every other failure propagates,
including a 403, which can come from an IP allow list or a gateway, so an outage is not shown to the user as
a bad token.

A 200 counts as an accepted token only when the body is a JSON object with a non-empty `accountId`, which Jira
Cloud's `myself` always returns. Any other 200, such as another JSON API or a gateway answering
`{"error":"login required"}`, throws `HttpRequestException` with that status, because the token was not
checked.

The email and API token are sent as UTF-8, so an email with a character outside ASCII reaches Jira intact.

Two answers say nothing about the token, so every call on `IJiraClient` and the `IExternalTaskProvider` for Jira
throws `HttpRequestException` with the response's own status for them:

| What happened | `StatusCode` |
|---|---|
| Base URL redirects (3xx: the site was renamed, or single sign-on stands in front of Jira). The message names the target's host and path. | The 3xx |
| Success without a JSON content type (a login page, a website, a proxy) | The success status, e.g. 200 |

`AddJiraIntegration` turns off redirects on the default primary handler, because .NET drops `Authorization` on
the next hop and a valid token would read as rejected. Setups it cannot reach:

- **A custom primary handler registered before `AddJiraIntegration`:** one that is not `SocketsHttpHandler` or
  `HttpClientHandler` stays as it is, and a warning is logged. Make sure it does not follow redirects.
- **`ConfigurePrimaryHttpMessageHandler` called after `AddJiraIntegration`, a `JiraClient` given an `HttpClient`
  made outside DI, or `JiraTaskProvider` built on your own `IHttpClientFactory`:** nothing warns, so set
  `AllowAutoRedirect = false` on the handler yourself.
