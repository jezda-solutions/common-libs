using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jezda.Common.Files.Storage;
using Jezda.Common.Files.Validation;

namespace Jezda.Common.Files.Services;

/// <summary>
/// Implements file operations with integrated validation, normalization, and storage.
/// Coordinates file validation, name policy enforcement, and storage operations.
/// </summary>
public class FileService : IFileService
{
    private readonly IFileValidator _validator;
    private readonly IFileStorage _storage;

    public FileService(IFileValidator validator, IFileStorage storage)
    {
        _validator = validator;
        _storage = storage;
    }

    /// <inheritdoc />
    public async Task<FileDescriptor> UploadAsync(Stream content, string fileName, string? subPath = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // The validator and storage both read the stream; without Seek the bytes the validator
        // consumed are gone before storage sees the stream, so a non-seekable one is refused.
        if (!content.CanSeek)
            throw new ArgumentException("The content stream must be seekable.", nameof(content));

        // Storage lays files out as subPath/yyyy/MM/dd; a blank subPath means none. A bad one
        // fails here, before the validator reads the stream.
        var targetSubPath = NormalizeSubPath(subPath);

        var startPosition = content.Position;

        // Validate file, including name policy, ZIP safety, executable detection, etc.
        var validation = await _validator.ValidateAsync(content, fileName, overrides: null, ct: cancellationToken);
        if (!validation.IsValid)
            throw new FileValidationException(validation.Errors ?? []);

        // The validator names the file with the configured FileValidationOptions.Policy, so storage gets the
        // same name the caller sees in the result. It skips the name check for a blank name; storage needs one.
        var normalizedName = validation.NormalizedFileName;
        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new FileValidationException(
                [new FileValidationError { Code = FileValidationErrorCode.InvalidFileName, Message = "Invalid file name." }]);

        // Store from where the caller's stream started, not from byte 0
        content.Seek(startPosition, SeekOrigin.Begin);

        var descriptor = await _storage.SaveAsync(content, normalizedName, targetSubPath, cancellationToken);
        descriptor.MimeType = validation.MimeType;
        return descriptor;
    }

    private static string? NormalizeSubPath(string? subPath)
    {
        if (string.IsNullOrWhiteSpace(subPath))
            return null;

        // Drop trailing separators and the whitespace between them, so "a/ /" ends as "a"
        var trimmed = subPath.Trim();
        while (trimmed.Length > 0 && (trimmed[^1] is '/' or '\\' || char.IsWhiteSpace(trimmed[^1])))
            trimmed = trimmed[..^1];

        // "/", " / " or "./" is not "no subPath": it is typically a path built from an empty tenant id,
        // and storing it in the shared root would hide that mistake. This segment rule runs here for
        // every IFileStorage backend; LocalFileStorage also rejects a sub-path that resolves to its root.
        if (trimmed.Split('/', '\\').All(segment => segment.Trim() is "" or "."))
            throw new ArgumentException("The sub-path must name a folder, not only separators or '.' segments.", nameof(subPath));

        return trimmed;
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        => _storage.OpenReadAsync(relativePath, cancellationToken);

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        => _storage.DeleteAsync(relativePath, cancellationToken);
}