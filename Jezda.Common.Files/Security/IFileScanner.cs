using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Files.Security;

/// <summary>
/// Defines a contract for scanning files for malware and security threats.
/// </summary>
public interface IFileScanner
{
    /// <summary>
    /// Scans the provided stream for malware or security threats.
    /// </summary>
    /// <param name="stream">The file stream to scan. Position will be restored after scanning if the stream is seekable.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task containing the scan result with findings and threat assessment.</returns>
    Task<FileScanResult> ScanAsync(Stream stream, CancellationToken ct = default);
}