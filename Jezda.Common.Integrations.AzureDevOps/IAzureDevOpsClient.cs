using Jezda.Common.Integrations.AzureDevOps.Models;

namespace Jezda.Common.Integrations.AzureDevOps;

public interface IAzureDevOpsClient
{
    /// <summary>
    /// Executes a WIQL query and returns a list of work items with their fields.
    /// This method automatically fetches details for the work items found by the query.
    /// </summary>
    Task<List<AdoWorkItem>> GetWorkItemsByQueryAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single work item by ID.
    /// </summary>
    Task<AdoWorkItem?> GetWorkItemByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new work item in the specified project.
    /// </summary>
    Task<AdoWorkItem?> CreateWorkItemAsync(string project, string type, Dictionary<string, object> fields, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves completed work log entries for work items modified within the specified date range.
    /// </summary>
    Task<IReadOnlyList<AdoWorkLogEntry>> GetCompletedWorkLogsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all projects in the organization.
    /// </summary>
    Task<List<AdoProject>> GetProjectsAsync(CancellationToken cancellationToken = default);
}
