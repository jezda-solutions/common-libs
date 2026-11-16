using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Jezda.Common.Files.Detection;
using Jezda.Common.Files.Security;
using Jezda.Common.Files.Validation;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jezda.Common.Files.Tests;

public class FileValidatorStartPositionTests
{
    private static FileValidator CreateValidator(long maxSizeBytes = 1024)
        => new(
            new MagicNumberMimeTypeDetector(),
            new NoopFileScanner(),
            Options.Create(new FileValidationOptions { MaxSizeBytes = maxSizeBytes }));

    [Fact]
    public async Task ValidateAsync_StreamAtEnd_ReportsEmptyFile()
    {
        using var ms = new MemoryStream(Encoding.ASCII.GetBytes("hello world"));
        ms.Position = ms.Length;

        var result = await CreateValidator().ValidateAsync(ms, "a.txt");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == FileValidationErrorCode.EmptyFile);
        Assert.Equal(ms.Length, ms.Position);
    }

    [Fact]
    public async Task ValidateAsync_LargeBufferWithShortTail_MeasuresRemainingBytes()
    {
        var data = new byte[4096];
        var tail = Encoding.ASCII.GetBytes("short tail text");
        tail.CopyTo(data, data.Length - tail.Length);
        using var ms = new MemoryStream(data);
        ms.Position = data.Length - tail.Length;

        var result = await CreateValidator(maxSizeBytes: 1024).ValidateAsync(ms, "a.txt");

        Assert.True(result.IsValid, string.Join(";", result.Errors.Select(e => e.Code)));
        Assert.Equal(tail.Length, result.SizeBytes);
        Assert.Equal(data.Length - tail.Length, ms.Position);
    }

    [Fact]
    public async Task ValidateAsync_DetectsMimeFromStartPosition()
    {
        var prefix = new byte[64];
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4 body");
        using var ms = new MemoryStream([.. prefix, .. pdf]);
        ms.Position = prefix.Length;

        var result = await CreateValidator().ValidateAsync(ms, "a.pdf");

        Assert.Equal("application/pdf", result.MimeType);
        Assert.Equal(prefix.Length, ms.Position);
    }

    [Fact]
    public async Task DetectAsync_ZipAtNonZeroStart_RestoresEntryPosition()
    {
        using var inner = new MemoryStream();
        using (var zip = new ZipArchive(inner, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write("<w:document/>");
        }
        var prefix = new byte[32];
        using var ms = new MemoryStream([.. prefix, .. inner.ToArray()]);
        ms.Position = prefix.Length;

        var mime = await new MagicNumberMimeTypeDetector().DetectAsync(ms);

        Assert.Equal("application/zip", mime);
        Assert.Equal(prefix.Length, ms.Position);
    }
}
