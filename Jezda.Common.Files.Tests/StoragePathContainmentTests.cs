using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Jezda.Common.Files.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jezda.Common.Files.Tests;

public sealed class StoragePathContainmentTests : IDisposable
{
    private readonly string _parent;
    private readonly string _root;
    private readonly LocalFileStorage _storage;

    public StoragePathContainmentTests()
    {
        _parent = Path.Combine(Path.GetTempPath(), "Jezda.Common.Files.Tests", Guid.NewGuid().ToString("N"));
        _root = Path.Combine(_parent, "root");
        _storage = new LocalFileStorage(Options.Create(new FileStorageOptions { RootPath = _root }));
    }

    public void Dispose()
    {
        try { Directory.Delete(_parent, recursive: true); } catch { /* ignore */ }
    }

    private static MemoryStream Content() => new(Encoding.UTF8.GetBytes("data"));

    [Theory]
    [InlineData("..")]
    [InlineData("../outside")]
    [InlineData("a/../../outside")]
    public async Task SaveAsync_SubPathEscapingRoot_Throws(string subPath)
    {
        await using var stream = Content();

        await Assert.ThrowsAsync<ArgumentException>(() => _storage.SaveAsync(stream, "doc.txt", subPath));

        Assert.False(Directory.Exists(Path.Combine(_parent, "outside")));
        Assert.Empty(Directory.GetFiles(_parent, "*", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task SaveAsync_RootedSubPath_Throws()
    {
        var rooted = Path.Combine(_parent, "elsewhere");
        await using var stream = Content();

        await Assert.ThrowsAsync<ArgumentException>(() => _storage.SaveAsync(stream, "doc.txt", rooted));

        Assert.False(Directory.Exists(rooted));
    }

    [Fact]
    public async Task SaveAsync_FileNameEscapingRoot_Throws()
    {
        // The storage prefixes "<guid>-", which turns the first ".." into a folder name; the next
        // four climb out of that folder and yyyy/MM/dd, and the sixth lands in the root's parent.
        await using var stream = Content();

        await Assert.ThrowsAsync<ArgumentException>(() => _storage.SaveAsync(stream, "../../../../../../escaped.txt"));

        Assert.Empty(Directory.GetFiles(_parent, "*escaped.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SaveAsync_NestedSubPath_StoresUnderRoot()
    {
        await using var stream = Content();

        var descriptor = await _storage.SaveAsync(stream, "doc.txt", "work-items/42");

        var fullPath = Path.GetFullPath(Path.Combine(_root, descriptor.RelativePath));
        Assert.StartsWith(Path.Combine(Path.GetFullPath(_root), "work-items", "42"), fullPath);
        Assert.True(File.Exists(fullPath));
    }

    [Fact]
    public async Task SaveAsync_SubPathReachingSiblingFolder_Throws()
    {
        // Stays inside the root but leaves tenant-a's folder for tenant-b's
        await using var stream = Content();

        await Assert.ThrowsAsync<ArgumentException>(() => _storage.SaveAsync(stream, "doc.txt", "tenant-a/../tenant-b"));

        Assert.False(Directory.Exists(Path.Combine(_root, "tenant-b")));
    }

    [Fact]
    public async Task SaveAsync_FileNameReachingSiblingFolder_Throws()
    {
        // The "<guid>-" prefix turns the first ".." into a folder name; the next four climb out of
        // it and yyyy/MM/dd, the fifth out of tenant-a, so the file would land in tenant-b
        await using var stream = Content();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _storage.SaveAsync(stream, "../../../../../../tenant-b/evil.txt", "tenant-a"));

        Assert.False(Directory.Exists(Path.Combine(_root, "tenant-b")));
    }

    [Fact]
    public async Task DeleteAsync_WithFilesystemRootAsRootPath_DoesNotThrow()
    {
        var fsRoot = Path.GetPathRoot(Path.GetTempPath())!;
        var storage = new LocalFileStorage(Options.Create(new FileStorageOptions { RootPath = fsRoot }));

        var deleted = await storage.DeleteAsync($"missing-{Guid.NewGuid():N}.txt");

        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteAsync_SiblingWithRootAsPrefix_ThrowsAndKeepsFile()
    {
        // "<parent>/root-other" starts with "<parent>/root" as a string but is outside the root
        var sibling = Path.Combine(_parent, "root-other");
        Directory.CreateDirectory(sibling);
        var victim = Path.Combine(sibling, "victim.txt");
        await File.WriteAllTextAsync(victim, "keep");

        await Assert.ThrowsAsync<ArgumentException>(() => _storage.DeleteAsync("../root-other/victim.txt"));

        Assert.True(File.Exists(victim));
    }

    [Fact]
    public async Task OpenReadAsync_RelativePathEscapingRoot_Throws()
    {
        Directory.CreateDirectory(_parent);
        await File.WriteAllTextAsync(Path.Combine(_parent, "secret.txt"), "secret");

        await Assert.ThrowsAsync<ArgumentException>(() => _storage.OpenReadAsync("../secret.txt"));
    }

    [Fact]
    public async Task DeleteAsync_RelativePathEscapingRoot_ThrowsAndKeepsFile()
    {
        Directory.CreateDirectory(_parent);
        var victim = Path.Combine(_parent, "victim.txt");
        await File.WriteAllTextAsync(victim, "keep");

        await Assert.ThrowsAsync<ArgumentException>(() => _storage.DeleteAsync("../victim.txt"));

        Assert.True(File.Exists(victim));
    }

    [Fact]
    public async Task DeleteAsync_RootedRelativePath_Throws()
    {
        Directory.CreateDirectory(_parent);
        var victim = Path.Combine(_parent, "victim.txt");
        await File.WriteAllTextAsync(victim, "keep");

        await Assert.ThrowsAsync<ArgumentException>(() => _storage.DeleteAsync(victim));

        Assert.True(File.Exists(victim));
    }
}
