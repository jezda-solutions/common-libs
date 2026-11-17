using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jezda.Common.Files.Detection;
using Jezda.Common.Files.Naming;
using Jezda.Common.Files.Security;
using Jezda.Common.Files.Services;
using Jezda.Common.Files.Storage;
using Jezda.Common.Files.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jezda.Common.Files.Tests;

public class FileServiceTests
{
    [Fact]
    public async Task UploadAsync_PassesSubPathToStorage()
    {
        var storage = new RecordingStorage();
        var service = new FileService(new AcceptingValidator(), storage);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        await service.UploadAsync(stream, "doc.txt", subPath: "work-items/42");

        Assert.Equal("work-items/42", storage.LastSubPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UploadAsync_BlankSubPath_StoresWithoutSubPath(string? subPath)
    {
        var storage = new RecordingStorage();
        var service = new FileService(new AcceptingValidator(), storage);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        await service.UploadAsync(stream, "doc.txt", subPath);

        Assert.Null(storage.LastSubPath);
    }

    [Fact]
    public async Task UploadAsync_ValidationFailure_MessageNamesTheErrors_NotTheTypes()
    {
        var validator = new FixedValidator(new FileValidationResult
        {
            IsValid = false,
            Errors =
            [
                new FileValidationError { Code = FileValidationErrorCode.TooLarge, Message = "File too big." },
                new FileValidationError { Code = FileValidationErrorCode.InvalidExtension, Message = "Extension blocked." }
            ]
        });
        var storage = new RecordingStorage();
        var service = new FileService(validator, storage);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        var ex = await Assert.ThrowsAsync<FileValidationException>(() => service.UploadAsync(stream, "doc.txt"));

        Assert.Equal("File too big.; Extension blocked.", ex.Message);
        Assert.Null(storage.Saved);
    }

    [Fact]
    public async Task UploadAsync_ValidationFailure_CarriesTheErrorCodes()
    {
        var validator = new FixedValidator(new FileValidationResult
        {
            IsValid = false,
            Errors =
            [
                new FileValidationError { Code = FileValidationErrorCode.TooLarge, Message = "File too big." },
                new FileValidationError { Code = FileValidationErrorCode.InvalidExtension, Message = "Extension blocked." }
            ]
        });
        var service = new FileService(validator, new RecordingStorage());

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        var ex = await Assert.ThrowsAsync<FileValidationException>(() => service.UploadAsync(stream, "doc.txt"));

        Assert.Equal(
            [FileValidationErrorCode.TooLarge, FileValidationErrorCode.InvalidExtension],
            ex.Errors.Select(e => e.Code));
    }

    // Callers that caught the old bare exception keep working.
    [Fact]
    public async Task UploadAsync_ValidationFailure_IsStillAnInvalidOperationException()
    {
        var validator = new FixedValidator(new FileValidationResult { IsValid = false });
        var service = new FileService(validator, new RecordingStorage());

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.UploadAsync(stream, "doc.txt"));

        var validationEx = Assert.IsType<FileValidationException>(ex);
        Assert.Equal("File validation failed.", validationEx.Message);
        Assert.Empty(validationEx.Errors);
    }

    [Fact]
    public async Task UploadAsync_StoresTheValidatorsNormalizedName()
    {
        var validator = new FixedValidator(new FileValidationResult { IsValid = true, NormalizedFileName = "normalized.txt" });
        var storage = new RecordingStorage();
        var service = new FileService(validator, storage);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        await service.UploadAsync(stream, "raw name.txt");

        Assert.Equal("normalized.txt", storage.LastFileName);
    }

    // A host that configures FileValidationOptions.Policy gets that policy's name stored, not the default one's.
    [Fact]
    public async Task UploadAsync_CustomPolicyInValidationOptions_NamesTheStoredFile()
    {
        var options = new FileValidationOptions { Policy = new FileNamePolicy { ReplacementChar = '-' } };
        var storage = new RecordingStorage();
        var service = new FileService(RealValidator(options), storage);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("plain text content"));
        await service.UploadAsync(stream, "a*b.txt");

        Assert.Equal("a-b.txt", storage.LastFileName);
    }

    [Fact]
    public async Task UploadAsync_NameThePolicyRejects_ThrowsInvalidFileName_NotStored()
    {
        var options = new FileValidationOptions { Policy = new FileNamePolicy { ReplaceInvalidChars = false } };
        var storage = new RecordingStorage();
        var service = new FileService(RealValidator(options), storage);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("plain text content"));
        var ex = await Assert.ThrowsAsync<FileValidationException>(() => service.UploadAsync(stream, "bad*name.txt"));

        Assert.Contains(ex.Errors, e => e.Code == FileValidationErrorCode.InvalidFileName);
        Assert.Null(storage.Saved);
    }

    // The validator treats the name as optional and skips the name check for a blank one; storage needs a name.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UploadAsync_BlankFileName_ThrowsInvalidFileName_NotStored(string fileName)
    {
        var storage = new RecordingStorage();
        var service = new FileService(RealValidator(new FileValidationOptions()), storage);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("plain text content"));
        var ex = await Assert.ThrowsAsync<FileValidationException>(() => service.UploadAsync(stream, fileName));

        Assert.Equal(FileValidationErrorCode.InvalidFileName, Assert.Single(ex.Errors).Code);
        Assert.Null(storage.Saved);
    }

    private static FileValidator RealValidator(FileValidationOptions options)
        => new(new MagicNumberMimeTypeDetector(), new NoopFileScanner(), Options.Create(options));

    [Fact]
    public async Task UploadAsync_NonSeekableStream_IsRejected_BeforeAnythingIsStored()
    {
        var storage = new RecordingStorage();
        var service = new FileService(new AcceptingValidator(), storage);

        await using var stream = new NonSeekableStream(Encoding.UTF8.GetBytes("data"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UploadAsync(stream, "doc.txt"));

        Assert.Null(storage.Saved);
    }

    [Fact]
    public async Task UploadAsync_NullStream_ThrowsArgumentNullException()
    {
        var service = new FileService(new AcceptingValidator(), new RecordingStorage());

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.UploadAsync(null!, "doc.txt"));
    }

    [Fact]
    public async Task UploadAsync_StreamStartingPastZero_StoresFromThatPosition()
    {
        var storage = new RecordingStorage();
        var service = new FileService(new ConsumingValidator(), storage);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("HEADERbody"));
        stream.Position = 6;
        await service.UploadAsync(stream, "doc.txt");

        Assert.Equal("body", Encoding.UTF8.GetString(storage.Saved!));
    }

    [Theory]
    [InlineData(" work-items/42 ", "work-items/42")]
    [InlineData("a/ /", "a")]
    [InlineData("work-items/42/", "work-items/42")]
    [InlineData("work-items\\42\\", "work-items\\42")]
    public async Task UploadAsync_SubPath_IsTrimmed(string subPath, string? expected)
    {
        var storage = new RecordingStorage();
        var service = new FileService(new AcceptingValidator(), storage);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        await service.UploadAsync(stream, "doc.txt", subPath);

        Assert.Equal(expected, storage.LastSubPath);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("\\")]
    [InlineData(" / ")]
    [InlineData("//")]
    [InlineData("/ /")]
    [InlineData(" \\ / ")]
    [InlineData(".")]
    [InlineData("./")]
    [InlineData(" . / . ")]
    [InlineData(".\\")]
    public async Task UploadAsync_SubPathNamingNoFolder_IsRejected_NotStored(string subPath)
    {
        var storage = new RecordingStorage();
        var service = new FileService(new AcceptingValidator(), storage);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UploadAsync(stream, "doc.txt", subPath));

        Assert.Equal("subPath", ex.ParamName);
        Assert.Null(storage.Saved);
    }

    [Fact]
    public async Task UploadAsync_SeparatorOnlySubPath_IsRejected_BeforeValidation()
    {
        var validator = new FixedValidator(new FileValidationResult { IsValid = false });
        var service = new FileService(validator, new RecordingStorage());

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UploadAsync(stream, "doc.txt", "/"));
    }

    private sealed class AcceptingValidator : IFileValidator
    {
        public Task<FileValidationResult> ValidateAsync(IFormFile file, FileValidationOverrides? overrides = null, CancellationToken ct = default)
            => Task.FromResult(new FileValidationResult { IsValid = true, MimeType = "text/plain" });

        public Task<FileValidationResult> ValidateAsync(Stream fileStream, string? fileName = null, FileValidationOverrides? overrides = null, CancellationToken ct = default)
            => Task.FromResult(new FileValidationResult { IsValid = true, MimeType = "text/plain", NormalizedFileName = fileName });
    }

    private sealed class FixedValidator(FileValidationResult result) : IFileValidator
    {
        public Task<FileValidationResult> ValidateAsync(IFormFile file, FileValidationOverrides? overrides = null, CancellationToken ct = default)
            => Task.FromResult(result);

        public Task<FileValidationResult> ValidateAsync(Stream fileStream, string? fileName = null, FileValidationOverrides? overrides = null, CancellationToken ct = default)
            => Task.FromResult(result);
    }

    /// <summary>Reads the stream to its end, like a validator that does not restore the position.</summary>
    private sealed class ConsumingValidator : IFileValidator
    {
        public Task<FileValidationResult> ValidateAsync(IFormFile file, FileValidationOverrides? overrides = null, CancellationToken ct = default)
            => Task.FromResult(new FileValidationResult { IsValid = true });

        public async Task<FileValidationResult> ValidateAsync(Stream fileStream, string? fileName = null, FileValidationOverrides? overrides = null, CancellationToken ct = default)
        {
            await fileStream.CopyToAsync(Stream.Null, ct);
            return new FileValidationResult { IsValid = true, NormalizedFileName = fileName };
        }
    }

    private sealed class RecordingStorage : IFileStorage
    {
        public string? LastSubPath { get; private set; } = "not-called";
        public string? LastFileName { get; private set; }
        public byte[]? Saved { get; private set; }

        public Task<FileDescriptor> SaveAsync(Stream content, string fileName, string? subPath = null, CancellationToken cancellationToken = default)
        {
            LastSubPath = subPath;
            LastFileName = fileName;
            using var copy = new MemoryStream();
            content.CopyTo(copy);
            Saved = copy.ToArray();
            return Task.FromResult(new FileDescriptor { Name = fileName, RelativePath = fileName });
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream());

        public Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public string? GetPublicUrl(string relativePath) => null;
    }
}
