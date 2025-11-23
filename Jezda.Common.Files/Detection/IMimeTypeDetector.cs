using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Files.Detection;

/// <summary>
/// Defines a contract for detecting MIME types from file content.
/// </summary>
public interface IMimeTypeDetector
{
    /// <summary>
    /// Detects the MIME type of a file by analyzing its content.
    /// </summary>
    /// <param name="stream">The file stream to analyze. Position will be restored after detection if the stream is seekable.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task containing the detected MIME type, or null if the type could not be determined.</returns>
    Task<string?> DetectAsync(Stream stream, CancellationToken ct = default);
}