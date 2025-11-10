using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Files.Storage;

public interface IFileStorage
{
    Task<FileDescriptor> SaveAsync(Stream content, string fileName, string? subPath = null, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    string? GetPublicUrl(string relativePath);
}