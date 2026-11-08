using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

    private sealed class AcceptingValidator : IFileValidator
    {
        public Task<FileValidationResult> ValidateAsync(IFormFile file, FileValidationOverrides? overrides = null, CancellationToken ct = default)
            => Task.FromResult(new FileValidationResult { IsValid = true, MimeType = "text/plain" });

        public Task<FileValidationResult> ValidateAsync(Stream fileStream, string? fileName = null, FileValidationOverrides? overrides = null, CancellationToken ct = default)
            => Task.FromResult(new FileValidationResult { IsValid = true, MimeType = "text/plain" });
    }

    private sealed class RecordingStorage : IFileStorage
    {
        public string? LastSubPath { get; private set; } = "not-called";

        public Task<FileDescriptor> SaveAsync(Stream content, string fileName, string? subPath = null, CancellationToken cancellationToken = default)
        {
            LastSubPath = subPath;
            return Task.FromResult(new FileDescriptor { Name = fileName, RelativePath = fileName });
        }

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream>(new MemoryStream());

        public Task<bool> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public string? GetPublicUrl(string relativePath) => null;
    }
}
