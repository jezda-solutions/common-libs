using FastEndpoints;
using System.Collections.Generic;
using System.Text.Json;

namespace Jezda.Common.Domain.Paged;

public class PagingInfo
{
    [QueryParam, BindFrom("current_page")]
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "current_page")]
    public int CurrentPage { get; set; } = 1;

    [QueryParam, BindFrom("page_size")]
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "page_size")]
    public int PageSize { get; set; } = 10;

    [QueryParam, BindFrom("sort_column")]
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "sort_column")]
    public string? SortColumn { get; set; }

    [QueryParam, BindFrom("sort_descending")]
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "sort_descending")]
    public bool SortDescending { get; set; } = false;

    [QueryParam, BindFrom("search_term")]
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "search_term")]
    public string? SearchTermJson { get; set; }

    [QueryParam, BindFrom("global_search")]
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "global_search")]
    public string? GlobalSearch { get; set; } = string.Empty;

    public Dictionary<string, string> SearchTerm =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(SearchTermJson ?? "{}") ?? [];

    [QueryParam, BindFrom("total_count")]
    [Microsoft.AspNetCore.Mvc.FromQuery(Name = "total_count")]
    public int TotalCount { get; set; } = 0;

    public int TotalPages => GetTotalPages(TotalCount);

    public int GetTotalPages(int totalCount)
    {
        var pageSize = PageSize;
        return (totalCount + pageSize - 1) / pageSize;
    }
}