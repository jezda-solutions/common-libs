using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Files.Security;

public sealed class NoopFileScanner : IFileScanner
{
    public Task<FileScanResult> ScanAsync(Stream stream, CancellationToken ct = default)
        => Task.FromResult(new FileScanResult { IsClean = true, Engine = "Noop", Report = "Not scanned" });
}