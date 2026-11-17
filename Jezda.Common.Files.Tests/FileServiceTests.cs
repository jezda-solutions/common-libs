using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jezda.Common.Files.Naming;
using Jezda.Common.Files.Services;
using Jezda.Common.Files.Storage;
using Jezda.Common.Files.Validation;
using Microsoft.AspNetCore.Http;
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
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(stream, "doc.txt"));

        Assert.Equal("File too big.; Extension blocked.", ex.Message);
        Assert.Null(storage.Saved);
    }

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
    public async Task UploadAsync_InvalidFileName_IsRejected_NotStored()
    {
        var storage = new RecordingStorage();
        var policy = new FileNamePolicy { ReplaceInvalidChars = false };
        var service = new FileService(new AcceptingValidator(), storage, policy);

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(stream, "bad*name.txt"));

        Assert.Null(storage.Saved);
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
    public async Task UploadAsync_SeparatorOnlySubPath_IsRejected_NotStored(string subPath)
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
            => Task.FromResult(new FileValidationResult { IsValid = true, MimeType = "text/plain" });
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
            return new FileValidationResult { IsValid = true };
        }
    }

    private sealed class RecordingStorage : IFileStorage
    {
        public string? LastSubPath { get; private set; } = "not-called";
        public byte[]? Saved { get; private set; }

        public Task<FileDescriptor> SaveAsync(Stream content, string fileName, string? subPath = null, CancellationToken cancellationToken = default)
        {
            LastSubPath = subPath;
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
