using FastEndpoints;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
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
    /// <exception cref="InvalidPagingParameterException">
    /// <c>search_term</c> is not a JSON object of strings. Use <see cref="TryGetSearchTerm"/> to check without throwing.
    /// </exception>
    /// <remarks>
    /// No binder fills it and no validator walks it: the raw value arrives through
    /// <see cref="SearchTermJson"/>. FastEndpoints would otherwise read <c>search_term</c> into this
    /// dictionary under its snake_case name, and MVC reads the getter while binding and validating, so
    /// an unreadable value failed before any handler ran.
    /// </remarks>
    [BindNever, ValidateNever]
    [DontBind(Source.QueryParam | Source.RouteParam | Source.FormField)]
    public Dictionary<string, string> SearchTerm =>
        TryGetSearchTerm(out var searchTerm, out var error)
            ? searchTerm
            : throw new InvalidPagingParameterException(
                PagingParameter.SearchTerm,
                column: null,
                "search_term must be a JSON object of strings, e.g. {\"name\":\"value\"}.",
                error);

    /// <summary>
    /// Reads <see cref="SearchTermJson"/> without throwing. An absent value and the JSON literal
    /// <c>null</c> both read as an empty map.
    /// </summary>
    /// <returns><see langword="false"/> when the value is not a JSON object of strings.</returns>
    public bool TryGetSearchTerm(out Dictionary<string, string> searchTerm) =>
        TryGetSearchTerm(out searchTerm, out _);

    private bool TryGetSearchTerm(out Dictionary<string, string> searchTerm, out JsonException? error)
    {
        error = null;

        if (SearchTermJson is null)
        {
            searchTerm = [];
            return true;
        }

        try
        {
            searchTerm = JsonSerializer.Deserialize<Dictionary<string, string>>(SearchTermJson) ?? [];
            return true;
        }
        catch (JsonException exception)
        {
            error = exception;
            searchTerm = [];
            return false;
        }
    }

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