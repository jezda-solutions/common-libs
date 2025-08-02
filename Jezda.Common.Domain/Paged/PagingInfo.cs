using FastEndpoints;
using System.Collections.Generic;
using System.Text.Json;

namespace Jezda.Common.Domain.Paged;

public class PagingInfo
{
    [QueryParam, BindFrom("currentPage")]
    public int CurrentPage { get; set; } = 1;

    [QueryParam, BindFrom("pageSize")]
    public int PageSize { get; set; } = 10;

    [QueryParam, BindFrom("sortColumn")]
    public string SortColumn { get; set; } = default!;

    [QueryParam, BindFrom("sortDescending")]
    public bool SortDescending { get; set; } = false;

    [QueryParam, BindFrom("searchTerm")]
    public string? SearchTermJson { get; set; }

    /// <summary>
    /// Gets or sets the global search query used to filter results.
    /// Responsible for filtering through all columns in the table
    /// </summary>
    [QueryParam, BindFrom("globalSearch")]
    public string? GlobalSearch { get; set; } = default!;

    /// <summary>
    /// Responsible for searching through one column (key in dictionary)
    /// by value of the dictionary row.
    /// </summary>
    public Dictionary<string, string>? SearchTerm =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(SearchTermJson ?? "{}");

    public int TotalCount { get; set; } = 0;

    public int TotalPages => GetTotalPages(TotalCount);

    public int GetTotalPages(int totalCount)
    {
        var pageSize = PageSize;
        return (totalCount + pageSize - 1) / pageSize;
    }
}