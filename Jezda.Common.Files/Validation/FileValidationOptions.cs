using Jezda.Common.Files.Naming;

namespace Jezda.Common.Files.Validation;

public sealed class FileValidationOptions
{
    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB default
    public string[]? AllowedMimeTypes { get; set; }
    public string[]? AllowedExtensions { get; set; }
    public string[]? BlockedExtensions { get; set; }
    public bool BlockExecutables { get; set; } = true;
    public bool EnableZipSafetyChecks { get; set; }
    public int ZipMaxDepth { get; set; } = 5;
    public int ZipMaxEntries { get; set; } = 1000;
    public double ZipMaxCompressionRatio { get; set; } = 200.0; // kompresija 200x
    public bool EnableImageDimensionChecks { get; set; }
    public int? MinWidth { get; set; }
    public int? MaxWidth { get; set; }
    public int? MinHeight { get; set; }
    public int? MaxHeight { get; set; }
    public FileNamePolicy Policy { get; set; } = new();
    public bool EnableHashing { get; set; }
    public bool EnableMalwareHeuristics { get; set; }
}