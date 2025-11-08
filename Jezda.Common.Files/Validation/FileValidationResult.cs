using System;
using System.Collections.Generic;

namespace Jezda.Common.Files.Validation;

public sealed class FileValidationResult
{
    public bool IsValid { get; set; }
    public string? MimeType { get; set; }
    public string? NormalizedFileName { get; set; }
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public IReadOnlyList<FileValidationError> Errors { get; set; } = Array.Empty<FileValidationError>();
}