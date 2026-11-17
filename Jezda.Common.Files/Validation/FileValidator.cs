using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Jezda.Common.Files.Detection;
using Jezda.Common.Files.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Jezda.Common.Files.Validation;

/// <summary>
/// Provides comprehensive file validation with configurable rules for MIME types, extensions,
/// file size, executables, ZIP bombs, image dimensions, hashing, and malware scanning.
/// </summary>
public sealed class FileValidator(IMimeTypeDetector mimeDetector, IFileScanner scanner, IOptions<FileValidationOptions> options) : IFileValidator
{
    /// <inheritdoc />
    public async Task<FileValidationResult> ValidateAsync(IFormFile file, FileValidationOverrides? overrides = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        // Awaited so the stream stays open until validation finishes, even when a check completes asynchronously.
        await using var stream = file.OpenReadStream();
        return await ValidateAsync(stream, file.FileName, overrides, ct);
    }

    /// <inheritdoc />
    public async Task<FileValidationResult> ValidateAsync(Stream fileStream, string? fileName = null, FileValidationOverrides? overrides = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        // Every check reads the stream and seeks back to the start position; without Seek the bytes
        // the first check consumed are gone, and size, hash and executable detection see the wrong content.
        if (!fileStream.CanSeek)
            throw new ArgumentException("The file stream must be seekable.", nameof(fileStream));

        var merged = MergeOptions(options.Value, overrides);
        var errors = new List<FileValidationError>();
        var result = new FileValidationResult();

        // Filename normalization
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var (isValidName, normalizedName) = merged.Policy.ValidateAndNormalize(fileName);
            if (!isValidName)
                errors.Add(new FileValidationError { Code = FileValidationErrorCode.InvalidFileName, Message = "Invalid file name." });
            result.NormalizedFileName = normalizedName;
        }

        // Size is what remains from the current position, the same bytes the storage will write.
        var size = Math.Max(0, fileStream.Length - fileStream.Position);
        if (size == 0)
            errors.Add(new FileValidationError { Code = FileValidationErrorCode.EmptyFile, Message = "File is empty." });
        if (size > merged.MaxSizeBytes)
            errors.Add(new FileValidationError { Code = FileValidationErrorCode.TooLarge, Message = $"File exceeds max size of {merged.MaxSizeBytes} bytes." });

        // Early exit if already too large
        if (errors.Any(e => e.Code == FileValidationErrorCode.TooLarge))
        {
            result.Errors = errors;
            result.IsValid = false;
            result.SizeBytes = size;
            return result;
        }

        // MIME detection
        var mime = await mimeDetector.DetectAsync(fileStream, ct);
        result.MimeType = mime;
        if (merged.AllowedMimeTypes is { Length: > 0 })
        {
            if (mime is null || !merged.AllowedMimeTypes.Contains(mime, StringComparer.OrdinalIgnoreCase))
                errors.Add(new FileValidationError { Code = FileValidationErrorCode.InvalidMimeType, Message = "MIME type not allowed." });
        }

        // Extension checks
        var ext = SafeExtension(result.NormalizedFileName ?? fileName);
        if (merged.AllowedExtensions is { Length: > 0 })
        {
            if (ext is null || !merged.AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                errors.Add(new FileValidationError { Code = FileValidationErrorCode.InvalidExtension, Message = "File extension not allowed." });
        }
        if (merged.BlockedExtensions is { Length: > 0 })
        {
            if (ext is not null && merged.BlockedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                errors.Add(new FileValidationError { Code = FileValidationErrorCode.InvalidExtension, Message = "File extension is blocked." });
        }

        // Executable detection
        if (merged.BlockExecutables && ExecutableDetector.IsExecutable(fileStream, result.NormalizedFileName ?? fileName))
        {
            errors.Add(new FileValidationError { Code = FileValidationErrorCode.ExecutableBlocked, Message = "Executable files are blocked." });
        }

        // Zip safety
        if (merged.EnableZipSafetyChecks && IsZipContainer(mime))
        {
            var safe = ZipSafetyChecker.IsSafe(fileStream, new ZipSafetyChecker.ZipSafetyOptions(merged.ZipMaxDepth, merged.ZipMaxEntries, merged.ZipMaxCompressionRatio));
            if (!safe)
                errors.Add(new FileValidationError { Code = FileValidationErrorCode.ZipBombSuspected, Message = "ZIP archive appears unsafe." });
        }

        // Image dimensions
        if (merged.EnableImageDimensionChecks && mime is not null)
        {
            var ok = ImageSafetyChecker.ValidateDimensions(fileStream, mime, new ImageSafetyChecker.ImageBounds(merged.MinWidth, merged.MaxWidth, merged.MinHeight, merged.MaxHeight));
            if (!ok)
                errors.Add(new FileValidationError { Code = FileValidationErrorCode.InvalidImageDimensions, Message = "Image dimensions out of allowed bounds." });
        }

        // Hashing
        if (merged.EnableHashing)
            result.Sha256 = await ComputeSha256Async(fileStream, ct);

        // Malware heuristics via scanner
        if (merged.EnableMalwareHeuristics)
        {
            var start = fileStream.Position;
            var scan = await scanner.ScanAsync(fileStream, ct);
            // The scanner is pluggable and need not rewind; the contract promises the position is restored.
            fileStream.Seek(start, SeekOrigin.Begin);
            if (!scan.IsClean)
                errors.Add(new FileValidationError { Code = FileValidationErrorCode.MalwareSuspected, Message = scan.Report ?? "Malware suspected." });
        }

        result.SizeBytes = size;
        result.Errors = errors;
        result.IsValid = errors.Count == 0;
        return result;
    }

    // The detector refines a ZIP to an Office Open XML type, but it is still a ZIP container and
    // must go through the same safety limits as a plain archive.
    private static bool IsZipContainer(string? mime)
        => string.Equals(mime, "application/zip", StringComparison.OrdinalIgnoreCase)
           || (mime is not null && mime.StartsWith("application/vnd.openxmlformats-officedocument.", StringComparison.OrdinalIgnoreCase));

    private static FileValidationOptions MergeOptions(FileValidationOptions options, FileValidationOverrides? overrides)
    {
        if (overrides is null) return options;
        return new FileValidationOptions
        {
            MaxSizeBytes = overrides.MaxSizeBytes ?? options.MaxSizeBytes,
            AllowedMimeTypes = overrides.AllowedMimeTypes ?? options.AllowedMimeTypes,
            AllowedExtensions = overrides.AllowedExtensions ?? options.AllowedExtensions,
            BlockedExtensions = overrides.BlockedExtensions ?? options.BlockedExtensions,
            BlockExecutables = overrides.BlockExecutables ?? options.BlockExecutables,
            EnableZipSafetyChecks = overrides.EnableZipSafetyChecks ?? options.EnableZipSafetyChecks,
            ZipMaxDepth = overrides.ZipMaxDepth ?? options.ZipMaxDepth,
            ZipMaxEntries = overrides.ZipMaxEntries ?? options.ZipMaxEntries,
            ZipMaxCompressionRatio = overrides.ZipMaxCompressionRatio ?? options.ZipMaxCompressionRatio,
            EnableImageDimensionChecks = overrides.EnableImageDimensionChecks ?? options.EnableImageDimensionChecks,
            MinWidth = overrides.MinWidth ?? options.MinWidth,
            MaxWidth = overrides.MaxWidth ?? options.MaxWidth,
            MinHeight = overrides.MinHeight ?? options.MinHeight,
            MaxHeight = overrides.MaxHeight ?? options.MaxHeight,
            Policy = options.Policy,
            EnableHashing = overrides.EnableHashing ?? options.EnableHashing,
            EnableMalwareHeuristics = overrides.EnableMalwareHeuristics ?? options.EnableMalwareHeuristics
        };
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken ct)
    {
        var pos = stream.Position;
        try
        {
            using var sha = SHA256.Create();
            var buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                sha.TransformBlock(buffer, 0, read, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }
        finally
        {
            stream.Seek(pos, SeekOrigin.Begin);
        }
    }

    private static string? SafeExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext)) return null;
        return ext.TrimStart('.').ToLowerInvariant();
    }
}