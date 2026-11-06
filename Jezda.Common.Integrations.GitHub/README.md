# Jezda.Common.Integrations.GitHub

A strongly-typed .NET client for GitHub REST API, built on top of `Jezda.Common.Integrations`.
Simplifies access to GitHub repositories and issues.

## ✨ Features

- **Repositories**: List repositories for the authenticated user.
- **Issues**: List issues for a specific repository.
- **Authentication**: Supports PAT (Personal Access Token) or OAuth tokens via Bearer auth.
- **Dependency Injection**: Ready-to-use `IServiceCollection` extension.

## 📦 Installation

Add the project reference or NuGet package:

```bash
dotnet add package Jezda.Common.Integrations.GitHub
```

## ⚙️ Configuration

Add the following section to your `appsettings.json`:

```json
{
  "Integrations": {
    "GitHub": {
      "BaseUrl": "https://api.github.com/",
      "AccessToken": "your-github-pat",
      "UserAgent": "Jezda-Integration-App"
    }
  }
}
```

> **Note**: GitHub API requires a valid `User-Agent` header.

## 🚀 Usage

### 1. Register Services
In your `Program.cs`:

```csharp
using Jezda.Common.Integrations.GitHub.Extensions;

builder.Services.AddGitHubIntegration(builder.Configuration);
```

### 2. Inject and Use
Inject `IGitHubClient` into your service:

```csharp
public class GitHubService
{
    private readonly IGitHubClient _gitHubClient;

    public GitHubService(IGitHubClient gitHubClient)
    {
        _gitHubClient = gitHubClient;
    }

    public async Task PrintIssuesAsync()
    {
        // 1. Get my repos
        var repos = await _gitHubClient.GetRepositoriesAsync();
        
        foreach(var repo in repos)
        {
            Console.WriteLine($"Repo: {repo.Name} ({repo.HtmlUrl})");

            // 2. Get issues for this repo
            var issues = await _gitHubClient.GetIssuesAsync(repo.Owner.Login, repo.Name);
            foreach(var issue in issues)
            {
                 Console.WriteLine($" - [{issue.State}] {issue.Title}");
            }
        }
    }
}
```

## Connection validation

`ValidateConnectionAsync` returns `false` only for 401, which is what GitHub answers a bad token with. Every
other failure propagates, including every 403, which can be a rate limit, a lockout after failed logins or an
address block, so none of them is shown to the user as a bad token.
