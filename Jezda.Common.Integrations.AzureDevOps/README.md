# Jezda.Common.Integrations.AzureDevOps

A strongly-typed .NET client for Azure DevOps REST API, built on top of `Jezda.Common.Integrations`. 
This library simplifies interaction with Azure DevOps by providing easy-to-use methods for Work Items, WIQL queries, and Time Tracking data.

## ✨ Features

- **Work Item Management**: Create and retrieve work items.
- **WIQL Support**: Execute raw WIQL queries and get typed results.
- **Time Tracking**: Extract completed work logs from work item history (reconstructed from `CompletedWork` field changes).
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

        // 3. Get time logs for January
        var logs = await _adoClient.GetCompletedWorkLogsAsync(
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2025, 1, 31, 23, 59, 59, TimeSpan.Zero)
        );
        
        foreach(var log in logs)
        {
            Console.WriteLine($"User {log.UserDisplayName} logged {log.Hours}h on {log.Project}");
        }
    }
}
```

## 🛠️ Key Models

- **`AdoWorkItem`**: Represents a simplified work item with generic `Fields` dictionary.
- **`AdoProject`**: Contains project details (Id, Name, Description, Url).
- **`AdoWorkLogEntry`**: Calculated work log entry (User, Hours, Date, Project).
