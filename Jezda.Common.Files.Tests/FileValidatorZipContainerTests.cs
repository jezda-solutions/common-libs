using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Jezda.Common.Files.Detection;
using Jezda.Common.Files.Security;
using Jezda.Common.Files.Validation;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jezda.Common.Files.Tests;

public class FileValidatorZipContainerTests
{
    private static FileValidator CreateValidator(int maxEntries)
        => new(
            new MagicNumberMimeTypeDetector(),
            new NoopFileScanner(),
            Options.Create(new FileValidationOptions
            {
                EnableZipSafetyChecks = true,
                ZipMaxEntries = maxEntries,
                BlockExecutables = false
            }));

    private static MemoryStream BuildZip(string firstEntry, int extraEntries)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, firstEntry);
            for (var i = 0; i < extraEntries; i++) Add(zip, $"extra/{i}.txt");
        }
        ms.Position = 0;
        return ms;
    }

    private static void Add(ZipArchive zip, string name)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8);
        writer.Write("x");
    }

    [Theory]
    [InlineData("word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("ppt/presentation.xml", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [InlineData("data/file.txt", "application/zip")]
    public async Task ValidateAsync_ZipContainerOverEntryLimit_IsRejectedWhateverItsDetectedType(string firstEntry, string expectedMime)
    {
        using var ms = BuildZip(firstEntry, extraEntries: 20);

        var result = await CreateValidator(maxEntries: 10).ValidateAsync(ms, "a.bin");

        Assert.Equal(expectedMime, result.MimeType);
        Assert.Contains(result.Errors, e => e.Code == FileValidationErrorCode.ZipBombSuspected);
    }

    [Fact]
    public async Task ValidateAsync_OfficeZipOverCompressionRatio_IsRejected()
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("word/document.xml", CompressionLevel.SmallestSize).Open());
            writer.Write(new string('a', 2_000_000));
        }
        ms.Position = 0;
        using var _ = ms;

        var result = await CreateValidator(maxEntries: 10).ValidateAsync(ms, "a.docx");

        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", result.MimeType);
        Assert.Contains(result.Errors, e => e.Code == FileValidationErrorCode.ZipBombSuspected);
    }

    [Fact]
    public async Task ValidateAsync_OfficeZipWithinLimits_Passes()
    {
        using var ms = BuildZip("word/document.xml", extraEntries: 2);

        var result = await CreateValidator(maxEntries: 10).ValidateAsync(ms, "a.docx");

        Assert.DoesNotContain(result.Errors, e => e.Code == FileValidationErrorCode.ZipBombSuspected);
    }
}
