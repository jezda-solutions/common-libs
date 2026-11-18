using System;
using System.Collections.Generic;
using System.Linq;

namespace Jezda.Common.Files.Validation;

/// <summary>
/// A file that failed validation on upload. <see cref="Errors"/> carries the codes, so a caller can
/// map them to a response, e.g. <see cref="FileValidationErrorCode.TooLarge"/> to 413.
/// </summary>
/// <remarks>
/// Derives from <see cref="InvalidOperationException"/> so code that already catches that keeps working.
/// </remarks>
public sealed class FileValidationException : InvalidOperationException
{
    public FileValidationException(IReadOnlyList<FileValidationError>? errors)
        : this(errors?.Select(e => new FileValidationError { Code = e.Code, Message = e.Message }).ToArray() ?? [])
    {
    }

    // A copy of the list and of each error, so a later change to either does not change the exception
    // or make Errors disagree with Message.
    private FileValidationException(FileValidationError[] errors)
        : base(BuildMessage(errors))
    {
        Errors = errors;
    }

    /// <summary>The validation errors, in the order the validator reported them; empty when it gave none.</summary>
    public IReadOnlyList<FileValidationError> Errors { get; }

    private static string BuildMessage(FileValidationError[] errors)
        => errors.Length > 0
            ? string.Join("; ", errors.Select(e => e.Message))
            : "File validation failed.";
}
