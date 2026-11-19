using System;
using System.IO;
using System.Linq;
using System.Text;
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
    // NAME_MAX on Linux (ext4 and most others) is 255 bytes; Windows allows 255 UTF-16 units,
    // which a 255-byte UTF-8 name never exceeds, so one byte limit covers both.
    private const int MaxStoredNameBytes = 255;

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
        // On Linux "\" is not a separator: "tenant-a\docs" would become one folder literally named
        // that, while GetPublicUrl turns "\" into "/" and points at a path that does not exist.
        // Treat it as a separator on every OS.
        if (!string.IsNullOrWhiteSpace(subPath))
        {
            subPath = subPath.Replace('\\', '/');
        }

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

        // "." or "./" resolves to the shared root rather than the caller's folder, and on Linux "\"
        // is a folder literally named "\"; a sub-path must name at least one real folder. The
        // resolved-path check is defense in depth behind the segment rules.
        // The string checks run first; ResolvesToRoot calls GetFullPath, which can throw on odd input.
        if (!string.IsNullOrWhiteSpace(subPath) && !NamesNoFolder(subPath) && HasBlankSegment(subPath))
        {
            throw new ArgumentException("The sub-path must not contain segments made only of dots and whitespace, other than a plain '.'.", nameof(subPath));
        }

        if (!string.IsNullOrWhiteSpace(subPath) && (NamesNoFolder(subPath) || ResolvesToRoot(subPath)))
        {
            throw new ArgumentException("The sub-path must name a folder, not only separators or '.' segments.", nameof(subPath));
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

        // Unique file name: <guid>-<original>, cut to fit the file system's name limit.
        // A blank name is stored as "file"; FileService rejects it before it gets here.
        var safeFileName = string.IsNullOrWhiteSpace(fileName) ? "file" : fileName;
        var uniqueName = FitStoredName($"{Guid.NewGuid():N}-", safeFileName);
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

    /// <summary>
    /// Joins <paramref name="prefix"/> and <paramref name="fileName"/>, shortening the base of the
    /// name so the result fits <see cref="MaxStoredNameBytes"/> UTF-8 bytes. The extension is kept
    /// unless it alone leaves no room for the base; the cut never splits a surrogate pair.
    /// </summary>
    private static string FitStoredName(string prefix, string fileName)
    {
        var budget = MaxStoredNameBytes - Encoding.UTF8.GetByteCount(prefix);
        if (Encoding.UTF8.GetByteCount(fileName) <= budget)
        {
            return prefix + fileName;
        }

        var extension = Path.GetExtension(fileName);
        var extensionBytes = Encoding.UTF8.GetByteCount(extension);
        if (extensionBytes >= budget)
        {
            extension = string.Empty;
            extensionBytes = 0;
        }

        var stem = fileName[..^extension.Length];
        // Windows drops trailing dots and spaces from a name, which would break the stored path
        var cut = TruncateUtf8(stem, budget - extensionBytes).TrimEnd('.', ' ');
        return prefix + cut + extension;
    }

    private static string TruncateUtf8(string value, int maxBytes)
    {
        var bytes = 0;
        var length = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            bytes += rune.Utf8SequenceLength;
            if (bytes > maxBytes)
            {
                break;
            }
            length += rune.Utf16SequenceLength;
        }
        return value[..length];
    }

    private string ResolveRelativePath(string relativePath)
        => EnsureUnderRoot(
            Path.Combine(_options.RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)),
            nameof(relativePath));

    /// <summary>
    /// True when every segment of <paramref name="subPath"/> (split on both separators) is empty,
    /// whitespace or ".", so it names no folder: "/", "\", ".", "./", " . / . ".
    /// </summary>
    private static bool NamesNoFolder(string subPath)
        => subPath.Split('/', '\\').All(segment => segment.Trim() is "" or ".");

    /// <summary>
    /// True when a segment of <paramref name="subPath"/> other than an empty or plain "." one is
    /// made only of dots and whitespace: " ", " . ", "...", ".. ". Linux keeps such a segment as a
    /// folder with that literal name; Windows trims trailing dots and spaces, so ".. " becomes ".."
    /// and would step into a sibling (another tenant's) folder past the exact ".." check.
    /// </summary>
    internal static bool HasBlankSegment(string subPath)
        => subPath.Split('/', '\\').Any(segment => segment is not ("" or ".") && segment.All(c => c == '.' || char.IsWhiteSpace(c)));

    private bool ResolvesToRoot(string subPath)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_options.RootPath));
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(_options.RootPath, subPath)));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(full, root, comparison);
    }

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