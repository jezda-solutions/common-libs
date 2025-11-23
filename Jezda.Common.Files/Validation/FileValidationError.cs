namespace Jezda.Common.Files.Validation;

/// <summary>
/// Represents a single validation error encountered during file validation.
/// </summary>
public sealed class FileValidationError
{
    /// <summary>
    /// Gets or sets the error code identifying the type of validation failure.
    /// </summary>
    public FileValidationErrorCode Code { get; set; }

    /// <summary>
    /// Gets or sets a human-readable description of the validation error.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}