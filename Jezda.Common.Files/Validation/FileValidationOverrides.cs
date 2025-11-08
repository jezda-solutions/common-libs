namespace Jezda.Common.Files.Validation;

public sealed class FileValidationOverrides
{
    public long? MaxSizeBytes { get; set; }
    public string[]? AllowedMimeTypes { get; set; }
    public string[]? AllowedExtensions { get; set; }
    public string[]? BlockedExtensions { get; set; }
    public bool? BlockExecutables { get; set; }
    public bool? EnableZipSafetyChecks { get; set; }
    public int? ZipMaxDepth { get; set; }
    public int? ZipMaxEntries { get; set; }
    public double? ZipMaxCompressionRatio { get; set; }
    public bool? EnableImageDimensionChecks { get; set; }
    public int? MinWidth { get; set; }
    public int? MaxWidth { get; set; }
    public int? MinHeight { get; set; }
    public int? MaxHeight { get; set; }
    public bool? EnableHashing { get; set; }
    public bool? EnableMalwareHeuristics { get; set; }
}