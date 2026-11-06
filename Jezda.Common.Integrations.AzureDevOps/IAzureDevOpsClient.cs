using Jezda.Common.Integrations.AzureDevOps.Models;

namespace Jezda.Common.Integrations.AzureDevOps;

public interface IAzureDevOpsClient
{
    /// <summary>
    /// Executes a WIQL query and returns a list of work items with their fields.
    /// This method automatically fetches details for the work items found by the query.
    /// </summary>
    /// <remarks>
    /// The result can hold fewer items than the query matched: an item deleted or made unreadable
    /// after the query ran is left out, with a warning in the log, instead of failing the call.
    /// </remarks>
    /// <exception cref="NotSupportedException">
    /// The query is a link query (<c>FROM WorkItemLinks</c>). Its results are links between work
    /// items, which this list cannot hold.
    /// </exception>
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
    /// <remarks>
    /// An entry is an increase of <c>Microsoft.VSTS.Scheduling.CompletedWork</c> in one update. The
    /// range and <see cref="AdoWorkLogEntry.LoggedAt"/> use the update's own
    /// <c>System.ChangedDate</c>, not its <c>revisedDate</c>, which is when the revision was superseded.
    /// </remarks>
    Task<IReadOnlyList<AdoWorkLogEntry>> GetCompletedWorkLogsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all projects in the organization.
    /// </summary>
    Task<List<AdoProject>> GetProjectsAsync(CancellationToken cancellationToken = default);
}
