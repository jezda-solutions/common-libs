using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jezda.Common.Files.Storage;

namespace Jezda.Common.Files.Services;

public interface IFileService
{
    Task<FileDescriptor> UploadAsync(Stream content, string fileName, string? tenantId = null, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}