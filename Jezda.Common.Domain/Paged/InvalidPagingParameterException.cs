using System;

namespace Jezda.Common.Domain.Paged;

/// <summary>
/// The paging parameter a caller sent that cannot be applied.
/// </summary>
public enum PagingParameter
{
    /// <summary><c>search_term</c>: not a JSON object of strings, or it names a column the list does not have.</summary>
    SearchTerm,

    /// <summary><c>sort_column</c>: names a column the list does not have.</summary>
    SortColumn
}

/// <summary>
/// Thrown when a caller-supplied paging parameter (<c>search_term</c> or <c>sort_column</c>) cannot be
/// applied to the list it was sent for.
/// </summary>
/// <remarks>
/// <para>
/// It marks a mistake in the request, not a fault of the service, so a consumer maps this one type to
/// a 400. Before it existed the same cases surfaced as a plain <see cref="ArgumentException"/> (unknown
/// column) or a <see cref="System.Text.Json.JsonException"/> (unreadable <c>search_term</c>), which a
/// consumer cannot tell apart from its own bugs and so answered as 500.
/// </para>
/// <para>
/// It derives from <see cref="ArgumentException"/> so code that already catches that keeps working.
/// A wrong <c>defaultSortColumn</c> passed by the consumer itself is a programming error and still
/// throws a plain <see cref="ArgumentException"/>.
/// </para>
/// </remarks>
public sealed class InvalidPagingParameterException : ArgumentException
{
    public InvalidPagingParameterException(PagingParameter parameter, string? column, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Parameter = parameter;
        Column = column;
    }

    /// <summary>Which query parameter was wrong.</summary>
    public PagingParameter Parameter { get; }

    /// <summary>
    /// The column name the caller sent, or <see langword="null"/> when <c>search_term</c> could not be read at all.
    /// </summary>
    public string? Column { get; }
}
