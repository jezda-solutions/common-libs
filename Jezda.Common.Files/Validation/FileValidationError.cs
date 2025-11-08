namespace Jezda.Common.Files.Validation;

public sealed class FileValidationError
{
    public FileValidationErrorCode Code { get; set; }
    public string Message { get; set; } = string.Empty;
}