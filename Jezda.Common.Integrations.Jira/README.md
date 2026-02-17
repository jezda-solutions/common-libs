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
