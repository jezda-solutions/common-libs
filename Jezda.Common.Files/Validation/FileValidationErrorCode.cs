namespace Jezda.Common.Files.Validation;

public enum FileValidationErrorCode
{
    TooLarge,
    EmptyFile,
    InvalidMimeType,
    InvalidExtension,
    ExecutableBlocked,
    ZipBombSuspected,
    InvalidImageDimensions,
    InvalidFileName,
    UnsupportedSignature,
    MalwareSuspected,
    ScannerError
}