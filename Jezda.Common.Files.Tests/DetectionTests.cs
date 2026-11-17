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

    // UTF-8 text in Serbian: Latin with diacritics and Cyrillic are multi-byte (non-ASCII) bytes,
    // repeated well past the 512-byte probe, so the data rows are inside it, not just the header.
    [Theory]
    [InlineData("Naziv;Cena\nČokolada đumbir;120\nŠećer žuti;95\nĆevapčići;450\nĐak ćuti;10\n")]
    [InlineData("Naziv;Cena\nЧоколада ђумбир;120\nШећер жути;95\nЋевапчићи;450\nЂак ћути;10\n")]
    [InlineData("Назив;Цена\nЧоколада;120\nШећер;95\n")]
    public async Task Utf8SerbianText_IsText(string sample)
    {
        var text = string.Concat(Enumerable.Repeat(sample, 20));
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var mime = await new MagicNumberMimeTypeDetector().DetectAsync(ms);

        Assert.Equal("text/plain", mime);
    }

    // Legacy code pages (Excel saves CSV in the ANSI code page on a Serbian Windows): not valid
    // UTF-8, still text.
    [Theory]
    [InlineData(1250, "Naziv;Cena\nČokolada đumbir;120\nŠećer žuti;95\nĆevapčići;450\n")]
    [InlineData(1251, "Naziv;Cena\nЧоколада ђумбир;120\nШећер жути;95\nЋевапчићи;450\n")]
    public async Task LegacyCodePageText_IsText(int codePage, string sample)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = Encoding.GetEncoding(codePage).GetBytes(string.Concat(Enumerable.Repeat(sample, 20)));
        using var ms = new MemoryStream(bytes);

        var mime = await new MagicNumberMimeTypeDetector().DetectAsync(ms);

        Assert.Equal("text/plain", mime);
    }

    [Fact]
    public async Task BinaryWithoutNullBytes_IsNotText()
    {
        // Every byte value 1..255 twice: no null byte, but ~10% control bytes, as in binary data.
        var data = Enumerable.Range(0, 510).Select(i => (byte)(i % 255 + 1)).ToArray();
        using var ms = new MemoryStream(data);

        var mime = await new MagicNumberMimeTypeDetector().DetectAsync(ms);

        Assert.Null(mime);
    }

    [Fact]
    public async Task ShortRiff_IsNotWebp()
    {
        // "RIFF" with fewer than 12 bytes cannot carry the WEBP fourCC at offset 8.
        using var ms = new MemoryStream([.. Encoding.ASCII.GetBytes("RIFF"), 0x01, 0x02]);

        var mime = await new MagicNumberMimeTypeDetector().DetectAsync(ms);

        Assert.NotEqual("image/webp", mime);
    }

    [Fact]
    public async Task ShortAsciiText_IsText()
    {
        using var ms = new MemoryStream(Encoding.ASCII.GetBytes("hi\n"));

        var mime = await new MagicNumberMimeTypeDetector().DetectAsync(ms);

        Assert.Equal("text/plain", mime);
    }
}