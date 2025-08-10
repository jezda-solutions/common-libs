using FastEndpoints;
using System.Collections.Generic;
using System.Text.Json;

namespace Jezda.Common.Domain.Paged;

public class PagingInfo
{
    /// <summary>
    /// Current page number in the paginated result set.
    /// </summary>
    [QueryParam, BindFrom("current_page")]
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "current_page")]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Specifies the number of items to return per page.
    /// </summary>
    [QueryParam, BindFrom("page_size")]
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "page_size")]
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Column name to sort by.
    /// </summary>
    [QueryParam, BindFrom("sort_column")]
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "sort_column")]
    public string? SortColumn { get; set; }

    /// <summary>
    /// Directs the sorting order of the column specified in SortColumn.
    /// </summary>
    [QueryParam, BindFrom("sort_descending")]
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "sort_descending")]
    public bool SortDescending { get; set; } = false;

    /// <summary>
    /// Responsible for searching through one column (key in dictionary)
    /// by value of the dictionary row.
    [QueryParam, BindFrom("search_term")]
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "search_term")]
    public string? SearchTermJson { get; set; }

    /// <summary>
    /// Gets or sets the global search query used to filter results.
    /// Responsible for filtering through all columns in the table
    /// </summary>
    [QueryParam, BindFrom("global_search")]
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "global_search")]
    public string? GlobalSearch { get; set; } = string.Empty;

    /// <summary>
    /// Parses the SearchTermJson into a dictionary. (key: column name, value: search term)
    /// </summary>
    public Dictionary<string, string> SearchTerm =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(SearchTermJson ?? "{}") ?? [];

    /// <summary>
    /// Total number of items in the result set. This is used to calculate the total number of pages.
    /// </summary>
    public int TotalCount { get; set; } = 0;

    /// <summary>
    /// Total number of pages in the result set.
    /// </summary>
    public int TotalPages => GetTotalPages(TotalCount);

    /// <summary>
    /// Calculates the total number of pages based on the total count of items and the page size.
    /// </summary>
    /// <param name="totalCount"></param>
    /// <returns></returns>
    public int GetTotalPages(int totalCount)
    {
        var pageSize = PageSize;
        return (totalCount + pageSize - 1) / pageSize;
    }
}