﻿using System;
using System.Linq;

namespace Jezda.Common.Domain.Paged;

/// <summary>
/// The paging parameter a caller sent that cannot be applied.
/// </summary>
public enum PagingParameter
{
    /// <summary><c>search_term</c>.</summary>
    SearchTerm,

    /// <summary><c>sort_column</c>.</summary>
    SortColumn
}

/// <summary>
/// Why a paging parameter cannot be applied.
/// </summary>
public enum InvalidPagingParameterReason
{
    /// <summary>The value cannot be read: <c>search_term</c> is not a JSON object of strings.</summary>
    Unreadable,

    /// <summary>The value names a column the list does not have, or one that is not a plain value.</summary>
    UnknownColumn
}

/// <summary>
/// A caller-supplied <c>search_term</c> or <c>sort_column</c> that cannot be applied to the list it was
/// sent for. It is the caller's mistake: map it to 400.
/// </summary>
/// <remarks>
/// Derives from <see cref="ArgumentException"/> so code that already catches that keeps working. A wrong
/// <c>defaultSortColumn</c> passed by the consumer itself is a programming error and stays a plain
/// <see cref="ArgumentException"/>.
/// </remarks>
public sealed class InvalidPagingParameterException : ArgumentException
{
    private const int MaxColumnLengthInMessage = 100;

    public InvalidPagingParameterException(
        PagingParameter parameter,
        InvalidPagingParameterReason reason,
        string? column = null,
        Exception? innerException = null)
        : base(BuildMessage(parameter, reason, column), innerException)
    {
        Parameter = parameter;
        Reason = reason;
        Column = column;
    }

    /// <summary>Which query parameter was wrong.</summary>
    public PagingParameter Parameter { get; }

    /// <summary>Why it cannot be applied.</summary>
    public InvalidPagingParameterReason Reason { get; }

    /// <summary>
    /// The column name exactly as the caller sent it, or <see langword="null"/> when the value could not be read.
    /// Caller input: never echo it into a response unescaped.
    /// </summary>
    public string? Column { get; }

    private static string BuildMessage(PagingParameter parameter, InvalidPagingParameterReason reason, string? column)
    {
        var name = parameter == PagingParameter.SearchTerm ? "search_term" : "sort_column";

        return reason == InvalidPagingParameterReason.Unreadable
            ? $"{name} must be a JSON object of strings, e.g. {{\"name\":\"value\"}}."
            : $"{name} names an unknown column '{ForMessage(column)}'.";
    }

    // The message lands in log lines: no control characters, bounded length.
    private static string ForMessage(string? column)
    {
        var clean = new string((column ?? string.Empty).Where(c => !char.IsControl(c)).ToArray());
        return clean.Length <= MaxColumnLengthInMessage ? clean : clean[..MaxColumnLengthInMessage] + "…";
    }
}
