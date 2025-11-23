using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Jezda.Common.Files.Validation;

/// <summary>
/// Defines a contract for comprehensive file validation including MIME type detection,
/// size checks, executable detection, ZIP safety, image dimensions, and malware scanning.
/// </summary>
public interface IFileValidator
{
    /// <summary>
    /// Validates an uploaded form file.
    /// </summary>
    /// <param name="file">The form file to validate.</param>
    /// <param name="overrides">Optional validation rule overrides for this specific file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task containing the validation result with errors and file metadata.</returns>
    Task<FileValidationResult> ValidateAsync(
        IFormFile file,
        FileValidationOverrides? overrides = null,
        CancellationToken ct = default);

    /// <summary>
    /// Validates a file stream with optional file name.
    /// </summary>
    /// <param name="fileStream">The file stream to validate. Position will be restored after validation if the stream is seekable.</param>
    /// <param name="fileName">Optional file name for extension-based validation.</param>
    /// <param name="overrides">Optional validation rule overrides for this specific file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task containing the validation result with errors and file metadata.</returns>
    Task<FileValidationResult> ValidateAsync(
        Stream fileStream,
        string? fileName = null,
        FileValidationOverrides? overrides = null,
        CancellationToken ct = default);
}