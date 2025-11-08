using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Files.Detection;

public interface IMimeTypeDetector
{
    Task<string?> DetectAsync(Stream stream, CancellationToken ct = default);
}