using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Jezda.Common.Files.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jezda.Common.Files.Tests;

public class StorageTests
{
    [Fact]
    public async Task LocalFileStorage_Save_Open_Delete_Works()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "Jezda.Common.Files.Tests", Guid.NewGuid().ToString("N"));
        var options = Options.Create(new FileStorageOptions
        {
            RootPath = tempRoot,
            PublicBaseUrl = "http://localhost/files",
            UseDateFolders = true
        });

        var storage = new LocalFileStorage(options);

        var content = "Hello world!";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var descriptor = await storage.SaveAsync(stream, "hello.txt", subPath: "dev");

        Assert.NotNull(descriptor);
        Assert.True(descriptor.Size > 0);
        Assert.EndsWith("hello.txt", descriptor.Name);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.RelativePath));

        // File exists on disk
        var fullPath = Path.Combine(tempRoot, descriptor.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath));

        // Read
        await using (var readStream = await storage.OpenReadAsync(descriptor.RelativePath))
        using (var reader = new StreamReader(readStream, Encoding.UTF8))
        {
            var read = await reader.ReadToEndAsync();
            Assert.Equal(content, read);
        }

        // Delete
        var deleted = await storage.DeleteAsync(descriptor.RelativePath);
        Assert.True(deleted);
        Assert.False(File.Exists(fullPath));

        // Cleanup test root
        try { Directory.Delete(tempRoot, recursive: true); } catch { /* ignore */ }
    }
}