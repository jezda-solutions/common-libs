using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jezda.Common.Files.Detection;
using Xunit;

namespace Jezda.Common.Files.Tests;

public class DetectionTests
{
    [Fact]
    public async Task Detects_PNG()
    {
        var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        using var ms = new MemoryStream();
        ms.Write(pngHeader);
        ms.Write(new byte[16]);
        ms.Position = 0;

        var det = new MagicNumberMimeTypeDetector();
        var mime = await det.DetectAsync(ms);
        Assert.Equal("image/png", mime);
    }

    [Fact]
    public async Task Detects_JPEG()
    {
        var jpgHeader = new byte[] { 0xFF, 0xD8, 0xFF };
        using var ms = new MemoryStream();
        ms.Write(jpgHeader);
        ms.Write(new byte[16]);
        ms.Position = 0;

        var det = new MagicNumberMimeTypeDetector();
        var mime = await det.DetectAsync(ms);
        Assert.Equal("image/jpeg", mime);
    }

    [Fact]
    public async Task Detects_PDF()
    {
        using var ms = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
        var det = new MagicNumberMimeTypeDetector();
        var mime = await det.DetectAsync(ms);
        Assert.Equal("application/pdf", mime);
    }

    [Fact]
    public async Task Detects_WEBP()
    {
        // RIFF + 4 bytes length + WEBP fourcc at offset 8
        using var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes("RIFF"));
        ms.Write(BitConverter.GetBytes(12)); // arbitrary length
        ms.Write(Encoding.ASCII.GetBytes("WEBP"));
        ms.Write(new byte[8]);
        ms.Position = 0;

        var det = new MagicNumberMimeTypeDetector();
        var mime = await det.DetectAsync(ms);
        Assert.Equal("image/webp", mime);
    }

    [Fact]
    public async Task Detects_DOCX_From_ZIP()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"></w:document>");
        }
        ms.Position = 0;

        var det = new MagicNumberMimeTypeDetector();
        var mime = await det.DetectAsync(ms);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", mime);
    }

    [Fact]
    public async Task BinaryWithAsciiHeader_IsNotText()
    {
        // 32 ASCII bytes, then binary data with null bytes: the text probe must look past the header.
        using var ms = new MemoryStream([.. Encoding.ASCII.GetBytes("CUSTOMFMT v1.0 header 0123456789"), .. new byte[64]]);

        var mime = await new MagicNumberMimeTypeDetector().DetectAsync(ms);

        Assert.Null(mime);
        Assert.Equal(0, ms.Position);
    }

    [Fact]
    public async Task AsciiText_LongerThanSignatureBuffer_IsText()
    {
        using var ms = new MemoryStream(Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("plain text line\n", 64))));

        var mime = await new MagicNumberMimeTypeDetector().DetectAsync(ms);

        Assert.Equal("text/plain", mime);
    }

    [Fact]
    public async Task ShortAsciiText_IsText()
    {
        using var ms = new MemoryStream(Encoding.ASCII.GetBytes("hi\n"));

        var mime = await new MagicNumberMimeTypeDetector().DetectAsync(ms);

        Assert.Equal("text/plain", mime);
    }
}