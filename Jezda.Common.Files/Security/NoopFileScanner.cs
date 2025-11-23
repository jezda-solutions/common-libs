using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Files.Security;

/// <summary>
/// A no-operation file scanner that always reports files as clean without performing any actual scanning.
/// Use this implementation when malware scanning is not required or for testing purposes.
/// </summary>
public sealed class NoopFileScanner : IFileScanner
{
    /// <inheritdoc />
    public Task<FileScanResult> ScanAsync(Stream stream, CancellationToken ct = default)
        => Task.FromResult(new FileScanResult { IsClean = true, Engine = "Noop", Report = "Not scanned" });
}