using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jezda.Common.Files.Naming;
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
    private readonly FileNamePolicy _namePolicy;

    public FileService(IFileValidator validator, IFileStorage storage, FileNamePolicy? namePolicy = null)
    {
        _validator = validator;
        _storage = storage;
        _namePolicy = namePolicy ?? new FileNamePolicy();
    }

    /// <inheritdoc />
    public async Task<FileDescriptor> UploadAsync(Stream content, string fileName, string? subPath = null, CancellationToken cancellationToken = default)
    {
        // The validator and storage both read the stream; without Seek the bytes the validator
        // consumed are gone before storage sees the stream, so a non-seekable one is refused.
        if (!content.CanSeek)
            throw new ArgumentException("The content stream must be seekable.", nameof(content));

        var startPosition = content.Position;

        // Validate file, including ZIP safety, executable detection, etc.
        var validation = await _validator.ValidateAsync(content, fileName, overrides: null, ct: cancellationToken);
        if (!validation.IsValid)
        {
            var msg = validation.Errors is { Count: > 0 }
                ? string.Join("; ", validation.Errors.Select(e => e.Message))
                : "File validation failed.";
            throw new InvalidOperationException(msg);
        }

        // Normalize file name via policy (Unicode-friendly); a name the policy rejects is not stored
        var (isValidName, normalizedName) = _namePolicy.ValidateAndNormalize(fileName);
        if (!isValidName)
            throw new InvalidOperationException("Invalid file name.");

        // Storage lays files out as subPath/yyyy/MM/dd; a blank subPath means none
        var targetSubPath = NormalizeSubPath(subPath);

        // Store from where the caller's stream started, not from byte 0
        content.Seek(startPosition, SeekOrigin.Begin);

        var descriptor = await _storage.SaveAsync(content, normalizedName, targetSubPath, cancellationToken);
        descriptor.MimeType = validation.MimeType;
        return descriptor;
    }

    private static string? NormalizeSubPath(string? subPath)
    {
        var trimmed = subPath?.Trim().TrimEnd('/', '\\').TrimEnd();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        => _storage.OpenReadAsync(relativePath, cancellationToken);

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        => _storage.DeleteAsync(relativePath, cancellationToken);
}