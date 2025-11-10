using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jezda.Common.Files.Naming;
using Jezda.Common.Files.Storage;
using Jezda.Common.Files.Validation;

namespace Jezda.Common.Files.Services;

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

    public async Task<FileDescriptor> UploadAsync(Stream content, string fileName, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        // Validate file, including ZIP safety, executable detection, etc.
        var validation = await _validator.ValidateAsync(content, fileName, overrides: null, ct: cancellationToken);
        if (!validation.IsValid)
        {
            var msg = validation.Errors is { Count: > 0 }
                ? string.Join("; ", validation.Errors)
                : "File validation failed.";
            throw new InvalidOperationException(msg);
        }

        // Normalize file name via policy (Unicode-friendly)
        var (_, normalizedName) = _namePolicy.ValidateAndNormalize(fileName);

        // Sub-path strategy: tenant/yyy/MM/dd (tenant optional)
        var subPath = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;

        // Reset stream position after validation when possible
        if (content.CanSeek)
        {
            content.Seek(0, SeekOrigin.Begin);
        }

        var descriptor = await _storage.SaveAsync(content, normalizedName, subPath, cancellationToken);
        descriptor.MimeType = validation.MimeType;
        return descriptor;
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
        => _storage.OpenReadAsync(relativePath, cancellationToken);

    public Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
        => _storage.DeleteAsync(relativePath, cancellationToken);
}