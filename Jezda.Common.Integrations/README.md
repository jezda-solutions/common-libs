# Jezda.Common.Integrations

Core library providing the foundation for external service integrations within the Jezda ecosystem. This library defines the base abstractions and shared logic for building strongly-typed, consistent integration clients.

## 🎯 Purpose

- **Standardization**: Enforces a consistent pattern for all external integrations (HTTP methods, logging, serialization).
- **Abstractions**: Provides base classes like `BaseIntegrationClient` to reduce boilerplate code.
- **Resilience**: Central place for handling common HTTP policies (can be extended with Polly).

## 📦 Components

### `BaseIntegrationClient`
An abstract base class that encapsulates `HttpClient` operations.

- **Methods**: `GetAsync`, `PostAsync`, `PutAsync`, `DeleteAsync`
- **Features**:
  - Automatic `System.Text.Json` serialization/deserialization (camelCase).
  - Built-in logging for request/response flow.
  - Standardized error handling (throws exceptions on non-success status codes).

## 🚀 How to Implement a New Integration

1. Create a new project (e.g., `Jezda.Common.Integrations.Jira`).
2. Reference this core library.
3. Inherit from `BaseIntegrationClient`.

```csharp
public class JiraClient : BaseIntegrationClient, IJiraClient
{
    public JiraClient(HttpClient httpClient, ILogger<JiraClient> logger) 
        : base(httpClient, logger)
    {
    }

    public async Task<Issue> GetIssueAsync(string key)
    {
        return await GetAsync<Issue>($"issue/{key}");
    }
}
```
