namespace Jezda.Common.Files.Validation;

/// <summary>
/// Defines error codes for file validation failures.
/// </summary>
public enum FileValidationErrorCode
{
    /// <summary>
    /// File size exceeds the maximum allowed size.
    /// </summary>
    TooLarge,

    /// <summary>
    /// File is empty (zero bytes).
    /// </summary>
    EmptyFile,

    /// <summary>
    /// File MIME type is not allowed or does not match expected types.
    /// </summary>
    InvalidMimeType,

    /// <summary>
    /// File extension is not allowed or is explicitly blocked.
    /// </summary>
    InvalidExtension,

    /// <summary>
    /// File is detected as an executable and executables are blocked.
    /// </summary>
    ExecutableBlocked,

    /// <summary>
    /// ZIP archive appears to be a ZIP bomb or violates safety constraints.
    /// </summary>
    ZipBombSuspected,

    /// <summary>
    /// Image dimensions are outside the allowed bounds.
    /// </summary>
    InvalidImageDimensions,

    /// <summary>
    /// File name contains invalid characters or violates naming policy.
    /// </summary>
    InvalidFileName,

    /// <summary>
    /// File signature is not supported or recognized.
    /// </summary>
    UnsupportedSignature,

    /// <summary>
    /// Malware or security threat was detected in the file.
    /// </summary>
    MalwareSuspected,

    /// <summary>
    /// An error occurred during malware scanning.
    /// </summary>
    ScannerError
}