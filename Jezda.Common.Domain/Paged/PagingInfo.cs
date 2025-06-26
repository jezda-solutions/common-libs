using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;

namespace Jezda.Common.Domain.Paged;

public class PagingInfo
{
    public const int DefaultCurrentPage = 1;

    public const int DefaultPageSize = 10;

    public const string DefaultSortColumn = default!;

    public const bool DefaultSortDescending = false;

    public static readonly Dictionary<string, string> DefaultSearchTerm = [];

    public const string DefaultGlobalSearch = default!;

    public int CurrentPage { get; set; } = DefaultCurrentPage;

    public int TotalPages { get; set; } = 0;

    public int PageSize { get; set; } = DefaultPageSize;

    public int TotalCount { get; set; } = 0;

    public string SortColumn { get; set; } = DefaultSortColumn;

    public Dictionary<string, string> SearchTerm { get; set; } = DefaultSearchTerm;

    public bool SortDescending { get; set; } = DefaultSortDescending;

    public string GlobalSearch { get; set; } = DefaultGlobalSearch;

    /// <summary>
    /// BindAsync is used to let API parse values from Query into Parameters of PagingInfo
    /// NEVER DELETE THIS!!!
    /// </summary>
    /// <param name="context"></param>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public static ValueTask<PagingInfo?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var result = new PagingInfo();

        var queryString = context.Request.QueryString.Value;
        if (string.IsNullOrEmpty(queryString))
            return ValueTask.FromResult<PagingInfo?>(result);

        var query = HttpUtility.ParseQueryString(queryString);

        if (int.TryParse(query["currentPage"], out int currentPage))
            result.CurrentPage = currentPage;

        if (int.TryParse(query["pageSize"], out int pageSize))
            result.PageSize = pageSize;

        if (int.TryParse(query["totalPages"], out int totalPages))
            result.TotalPages = totalPages;

        if (int.TryParse(query["totalCount"], out int totalCount))
            result.TotalCount = totalCount;

        result.SortColumn = query["sortColumn"] ?? string.Empty;

        if (bool.TryParse(query["sortDescending"], out bool sortDescending))
            result.SortDescending = sortDescending;

        foreach (var key in query.AllKeys)
        {
            if (key?.StartsWith("searchTerm.", StringComparison.OrdinalIgnoreCase) == true)
            {
                string dictKey = key["searchTerm.".Length..];
                string dictValue = query[key] ?? string.Empty;
                result.SearchTerm[dictKey] = dictValue;
            }
        }

        return ValueTask.FromResult<PagingInfo?>(result);
    }
}