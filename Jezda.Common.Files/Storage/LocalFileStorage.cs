using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jezda.Common.Files.Detection;
using Microsoft.Extensions.Options;

namespace Jezda.Common.Files.Storage;

/// <summary>
/// Implements file storage on the local file system with configurable directory structure
/// and optional public URL support. Files are organized by sub-path and optional date folders (yyyy/MM/dd).
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly FileStorageOptions _options;
    private readonly IMimeTypeDetector? _mimeTypeDetector;

    public LocalFileStorage(IOptions<FileStorageOptions> options, IMimeTypeDetector? mimeTypeDetector = null)
    {
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _mimeTypeDetector = mimeTypeDetector;

        if (string.IsNullOrWhiteSpace(_options.RootPath))
        {
            throw new ArgumentException("FileStorageOptions.RootPath must be configured.");
        }
        Directory.CreateDirectory(_options.RootPath);
    }

    /// <inheritdoc />
    public async Task<FileDescriptor> SaveAsync(Stream content, string fileName, string? subPath = null, CancellationToken cancellationToken = default)
    {
        // A rooted subPath, a ".." in it or a directory part in fileName could reach another
        // sub-path's folder (another tenant's) while still staying inside the root, so all are refused
        if (!string.IsNullOrWhiteSpace(subPath)
            && (Path.IsPathRooted(subPath) || subPath.Split('/', '\\').Contains("..")))
        {
            throw new ArgumentException("The sub-path must be relative and must not contain '..' segments.", nameof(subPath));
        }

        if (!string.IsNullOrWhiteSpace(fileName) && Path.GetFileName(fileName) != fileName)
        {
            throw new ArgumentException("The file name must not contain a directory part.", nameof(fileName));
        }

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

        basePath = EnsureUnderRoot(basePath, nameof(subPath));
        Directory.CreateDirectory(basePath);

        // Unique file name: <guid>-<original>
        var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "file" : fileName;
        var uniqueName = $"{Guid.NewGuid():N}-{safeFileName}";
        var fullPath = EnsureUnderRoot(Path.Combine(basePath, uniqueName), nameof(fileName));

        // Detect MIME type before saving
        string? mimeType = null;
        if (_mimeTypeDetector != null && content.CanSeek)
        {
            try
            {
                mimeType = await _mimeTypeDetector.DetectAsync(content, cancellationToken);
            }
            catch
            {
                // Ignore detection errors and fall back to extension-based detection
            }
        }

        // Fall back to extension-based detection if magic number detection failed or is unavailable
        if (string.IsNullOrEmpty(mimeType))
        {
            mimeType = ExtensionBasedMimeTypeDetector.Detect(safeFileName);
        }

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
            MimeType = mimeType,
            Size = size,
            RelativePath = relativePath,
            PublicUrl = GetPublicUrl(relativePath)
        };

        return descriptor;
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveRelativePath(relativePath);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveRelativePath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    private string ResolveRelativePath(string relativePath)
        => EnsureUnderRoot(
            Path.Combine(_options.RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)),
            nameof(relativePath));

    /// <summary>
    /// Resolves <paramref name="path"/> and throws unless it is the storage root or lies inside it,
    /// so a sub-path, file name or relative path with <c>..</c> segments or a rooted path cannot
    /// reach files outside <see cref="FileStorageOptions.RootPath"/>.
    /// </summary>
    private string EnsureUnderRoot(string path, string paramName)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_options.RootPath));
        var full = Path.GetFullPath(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        // A drive or filesystem root (D:\ or /) keeps its separator after trimming
        var rootPrefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;

        if (string.Equals(full, root, comparison) || full.StartsWith(rootPrefix, comparison))
        {
            return full;
        }

        throw new ArgumentException("The path resolves outside the storage root.", paramName);
    }

    /// <inheritdoc />
    public string? GetPublicUrl(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl)) return null;
        var urlPath = relativePath.Replace("\\", "/");
        return _options.PublicBaseUrl!.TrimEnd('/') + "/" + urlPath;
    }
}