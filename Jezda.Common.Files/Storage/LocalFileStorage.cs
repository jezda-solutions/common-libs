using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Jezda.Common.Files.Storage;

/// <summary>
/// Implements file storage on the local file system with configurable directory structure
/// and optional public URL support. Files are organized by sub-path and optional date folders (yyyy/MM/dd).
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly FileStorageOptions _options;

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(_options.RootPath))
        {
            throw new ArgumentException("FileStorageOptions.RootPath must be configured.");
        }
        Directory.CreateDirectory(_options.RootPath);
    }

    /// <inheritdoc />
    public async Task<FileDescriptor> SaveAsync(Stream content, string fileName, string? subPath = null, CancellationToken cancellationToken = default)
    {
        // Create hierarchy: Root/subPath/(yyyy/MM/dd)/
        var basePath = _options.RootPath;
        if (!string.IsNullOrWhiteSpace(subPath))
        {
            basePath = Path.Combine(basePath, subPath);
        }

        if (_options.UseDateFolders)
        {
            var now = DateTime.UtcNow;
            basePath = Path.Combine(basePath, now.ToString("yyyy"), now.ToString("MM"), now.ToString("dd"));
        }

        Directory.CreateDirectory(basePath);

        // Unique file name: <guid>-<original>
        var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "file" : fileName;
        var uniqueName = $"{Guid.NewGuid():N}-{safeFileName}";
        var fullPath = Path.Combine(basePath, uniqueName);

        long size = 0;
        using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await content.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                size += read;
            }
        }

        var relativePath = Path.GetRelativePath(_options.RootPath, fullPath);
        var descriptor = new FileDescriptor
        {
            Name = safeFileName,
            Size = size,
            RelativePath = relativePath,
            PublicUrl = GetPublicUrl(relativePath)
        };

        return descriptor;
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_options.RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_options.RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public string? GetPublicUrl(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl)) return null;
        var urlPath = relativePath.Replace("\\", "/");
        return _options.PublicBaseUrl!.TrimEnd('/') + "/" + urlPath;
    }
}