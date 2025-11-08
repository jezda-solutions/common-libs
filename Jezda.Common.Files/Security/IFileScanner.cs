using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Files.Security;

public interface IFileScanner
{
    Task<FileScanResult> ScanAsync(Stream stream, CancellationToken ct = default);
}