using System;
using System.Collections.Generic;

namespace Jezda.Common.Files.Validation;

/// <summary>
/// Represents the result of file validation including detected metadata and any errors.
/// </summary>
public sealed class FileValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the file passed all validation checks.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the detected MIME type of the file.
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// Gets or sets the normalized file name after applying naming policy rules.
    /// </summary>
    public string? NormalizedFileName { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the file content, if hashing was enabled.
    /// </summary>
    public string? Sha256 { get; set; }

    /// <summary>
    /// Gets or sets the collection of validation errors encountered during validation.
    /// </summary>
    public IReadOnlyList<FileValidationError> Errors { get; set; } = [];
}