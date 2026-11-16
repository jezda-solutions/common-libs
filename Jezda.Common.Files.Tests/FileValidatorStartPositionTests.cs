using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text;
using System.Threading;
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

    private static byte[] BuildZip(string entryName, int extraEntries = 0)
    {
        using var inner = new MemoryStream();
        using (var zip = new ZipArchive(inner, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, entryName);
            for (var i = 0; i < extraEntries; i++)
                AddEntry(zip, $"extra/{i}.txt");
        }
        return inner.ToArray();
    }

    private static void AddEntry(ZipArchive zip, string name)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8);
        writer.Write("<w:document/>");
    }

    [Fact]
    public async Task DetectAsync_OfficeZipAtNonZeroStart_DetectsOfficeTypeAndRestoresPosition()
    {
        var prefix = new byte[32];
        using var ms = new MemoryStream([.. prefix, .. BuildZip("word/document.xml")]);
        ms.Position = prefix.Length;

        var mime = await new MagicNumberMimeTypeDetector().DetectAsync(ms);

        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", mime);
        Assert.Equal(prefix.Length, ms.Position);
    }

    [Fact]
    public async Task ValidateAsync_SafeZipAtNonZeroStart_IsValidWithHashing()
    {
        var prefix = new byte[32];
        using var ms = new MemoryStream([.. prefix, .. BuildZip("word/document.xml")]);
        ms.Position = prefix.Length;
        var validator = new FileValidator(
            new MagicNumberMimeTypeDetector(),
            new NoopFileScanner(),
            Options.Create(new FileValidationOptions { EnableZipSafetyChecks = true, EnableHashing = true, BlockExecutables = false }));

        var result = await validator.ValidateAsync(ms, "a.docx");

        Assert.True(result.IsValid, string.Join(";", result.Errors.Select(e => e.Code)));
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.MimeType);
        Assert.NotNull(result.Sha256);
        Assert.Equal(ms.Length - prefix.Length, result.SizeBytes);
        Assert.Equal(prefix.Length, ms.Position);
    }

    // The 6-entry archive is safe under a limit of 6 and a bomb under 5: only the entry count
    // decides, so an unreadable archive (reported as a bomb either way) cannot pass both cases.
    [Theory]
    [InlineData(6, true)]
    [InlineData(5, false)]
    public async Task ValidateAsync_ZipAtNonZeroStart_AppliesEntryLimit(int maxEntries, bool expectedValid)
    {
        var prefix = new byte[32];
        using var ms = new MemoryStream([.. prefix, .. BuildZip("word/document.xml", extraEntries: 5)]);
        ms.Position = prefix.Length;
        var validator = new FileValidator(
            new MagicNumberMimeTypeDetector(),
            new NoopFileScanner(),
            Options.Create(new FileValidationOptions { EnableZipSafetyChecks = true, ZipMaxEntries = maxEntries, BlockExecutables = false }));

        var result = await validator.ValidateAsync(ms, "a.docx");

        Assert.Equal(expectedValid, result.IsValid);
        Assert.Equal(!expectedValid, result.Errors.Any(e => e.Code == FileValidationErrorCode.ZipBombSuspected));
        Assert.Equal(prefix.Length, ms.Position);
    }

    [Fact]
    public async Task DetectAsync_ShortReads_StillDetectsPng()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0];
        using var stream = new OneByteAtATimeStream(png);

        var mime = await new MagicNumberMimeTypeDetector().DetectAsync(stream);

        Assert.Equal("image/png", mime);
    }

    private sealed class OneByteAtATimeStream(byte[] data) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
            => base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], ct);
    }
}
