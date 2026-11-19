using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Jezda.Common.Files.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jezda.Common.Files.Tests;

public sealed class StorageFileNameLengthTests : IDisposable
{
    // NAME_MAX on Linux is 255 bytes; LocalFileStorage prefixes "<guid:N>-" (33 bytes).
    private const int MaxNameBytes = 255;

    private readonly string _root;
    private readonly LocalFileStorage _storage;

    public StorageFileNameLengthTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Jezda.Common.Files.Tests", Guid.NewGuid().ToString("N"));
        _storage = new LocalFileStorage(Options.Create(new FileStorageOptions { RootPath = _root }));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    private static MemoryStream Content() => new(Encoding.UTF8.GetBytes("data"));

    private string StoredName(FileDescriptor descriptor)
    {
        var fullPath = Path.Combine(_root, descriptor.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath));
        return Path.GetFileName(fullPath);
    }

    [Fact]
    public async Task SaveAsync_NameThatFitsAfterPrefix_IsStoredUnchanged()
    {
        var fileName = new string('a', MaxNameBytes - 33 - 4) + ".pdf";
        await using var stream = Content();

        var descriptor = await _storage.SaveAsync(stream, fileName);

        var stored = StoredName(descriptor);
        Assert.Equal(MaxNameBytes, Encoding.UTF8.GetByteCount(stored));
        Assert.EndsWith("-" + fileName, stored);
    }

    [Fact]
    public async Task SaveAsync_AsciiNameTooLongForPrefix_TruncatesBaseAndKeepsExtension()
    {
        var fileName = new string('a', 236) + ".pdf";
        await using var stream = Content();

        var descriptor = await _storage.SaveAsync(stream, fileName);

        var stored = StoredName(descriptor);
        Assert.Equal(MaxNameBytes, Encoding.UTF8.GetByteCount(stored));
        Assert.EndsWith(".pdf", stored);
        Assert.Equal(fileName, descriptor.Name);
        Assert.Equal("application/pdf", descriptor.MimeType);
    }

    [Fact]
    public async Task SaveAsync_MultiByteNameUnderCharLimit_FitsInBytes()
    {
        // 200 chars pass a 255-char policy, but Cyrillic takes 2 bytes per char in UTF-8
        var fileName = new string('ш', 200) + ".txt";
        await using var stream = Content();

        var descriptor = await _storage.SaveAsync(stream, fileName);

        var stored = StoredName(descriptor);
        Assert.True(Encoding.UTF8.GetByteCount(stored) <= MaxNameBytes);
        Assert.EndsWith("ш.txt", stored);
    }

    [Fact]
    public async Task SaveAsync_SurrogatePairsAtCut_AreNotSplit()
    {
        var fileName = string.Concat(System.Linq.Enumerable.Repeat("😀", 100)) + ".png";
        await using var stream = Content();

        var descriptor = await _storage.SaveAsync(stream, fileName);

        var stored = StoredName(descriptor);
        Assert.True(Encoding.UTF8.GetByteCount(stored) <= MaxNameBytes);
        Assert.EndsWith("😀.png", stored);
        Assert.DoesNotContain('�', stored);
        Assert.False(char.IsSurrogate(stored[^5]) && !char.IsSurrogatePair(stored[^6], stored[^5]));
    }

    [Fact]
    public async Task SaveAsync_ExtensionTooLongToKeep_TruncatesWholeName()
    {
        var fileName = "a." + new string('x', 300);
        await using var stream = Content();

        var descriptor = await _storage.SaveAsync(stream, fileName);

        var stored = StoredName(descriptor);
        Assert.Equal(MaxNameBytes, Encoding.UTF8.GetByteCount(stored));
        Assert.StartsWith("a.xxx", stored[33..]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_BlankName_FallsBackToFile(string fileName)
    {
        await using var stream = Content();

        var descriptor = await _storage.SaveAsync(stream, fileName);

        Assert.Equal("file", descriptor.Name);
        Assert.EndsWith("-file", StoredName(descriptor));
    }
}
