using Jezda.Common.Integrations.AzureDevOps.Models;
using Microsoft.Extensions.Logging;

namespace Jezda.Common.Integrations.AzureDevOps;

/// <summary>
/// Hydrates work item ids into full work items, for both Azure DevOps clients: the typed
/// <see cref="IAzureDevOpsClient"/> and the named client behind <c>AzureDevOpsTaskProvider</c>.
/// </summary>
internal static class AzureDevOpsWorkItemBatch
{
    /// <summary>
    /// The most ids Azure DevOps accepts on a single <c>workitems?ids=</c> request. Beyond it the
    /// call fails outright rather than truncating.
    /// </summary>
    private const int MaxIdsPerRequest = 200;

    /// <summary>
    /// The most missing ids a warning names, so a lost project does not write thousands into one line.
    /// </summary>
    private const int MaxIdsInWarning = 20;

    /// <summary>
    /// Fetches the ids in batches through <paramref name="getBatch"/>, which sends the request and
    /// applies its client's own error handling.
    /// </summary>
    /// <remarks>
    /// <c>errorPolicy=omit</c> because an id deleted or made unreadable after the WIQL ran would
    /// otherwise fail the whole batch with 404. Azure DevOps answers such an id with a null slot,
    /// which is dropped here, so the result can hold fewer items than ids were asked for. A warning
    /// records the difference, because a token that lost access to some items looks the same as
    /// those items being gone. It names the organisation, since one host can serve several, and the
    /// missing ids, which are what lets someone check whether those items still exist.
    /// </remarks>
    /// <exception cref="HttpRequestException">
    /// A batch was answered without a list. Omitted items are null slots in a list, so a missing list
    /// says nothing about them, and reading it as omitted would lose up to a whole batch.
    /// <see cref="HttpRequestException.StatusCode"/> is <see langword="null"/>: the answer was a
    /// success, so the status says nothing, and the call failed for the same reason an outage does.
    /// </exception>
    public static async Task<List<AdoWorkItem>> FetchAsync(
        IEnumerable<int> ids,
        string apiVersion,
        string? organisation,
        Func<string, CancellationToken, Task<AdoWorkItemListResponse?>> getBatch,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var workItems = new List<AdoWorkItem>();
        var requested = new List<int>();

        foreach (var batch in ids.Chunk(MaxIdsPerRequest))
        {
            requested.AddRange(batch);

            var url = $"_apis/wit/workitems?ids={string.Join(",", batch)}&errorPolicy=omit&api-version={apiVersion}";
            var response = await getBatch(url, cancellationToken);

            if (response?.Value is null)
            {
                throw new HttpRequestException(
                    $"Azure DevOps answered a batch of {batch.Length} work items (first id {batch[0]}, last id {batch[^1]}) "
                    + $"on {organisation} without a list of them, so their state is unknown.");
            }

            workItems.AddRange(response.Value.OfType<AdoWorkItem>());
        }

        // By id, not by count: a duplicate in either list would otherwise hide a missing item.
        var missing = requested.Except(workItems.Select(workItem => workItem.Id)).ToList();

        if (missing.Count > 0)
        {
            logger.LogWarning(
                "Azure DevOps returned {Returned} of {Requested} requested work items on {Organisation}. The rest were deleted or the token cannot read them. Missing ids: {MissingIds}",
                workItems.Count,
                requested.Distinct().Count(),
                organisation,
                string.Join(", ", missing.Take(MaxIdsInWarning))
                    + (missing.Count > MaxIdsInWarning ? $" and {missing.Count - MaxIdsInWarning} more" : string.Empty));
        }

        return workItems;
    }
}
