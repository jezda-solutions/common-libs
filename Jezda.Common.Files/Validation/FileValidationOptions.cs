using Jezda.Common.Files.Naming;

namespace Jezda.Common.Files.Validation;

/// <summary>
/// Configuration options for file validation behavior.
/// </summary>
public sealed class FileValidationOptions
{
    /// <summary>
    /// Gets or sets the maximum allowed file size in bytes. Default is 10 MB.
    /// </summary>
    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the allowed MIME types. If null or empty, all MIME types are allowed.
    /// </summary>
    public string[]? AllowedMimeTypes { get; set; }

    /// <summary>
    /// Gets or sets the allowed file extensions. If null or empty, all extensions are allowed.
    /// </summary>
    public string[]? AllowedExtensions { get; set; }

    /// <summary>
    /// Gets or sets the explicitly blocked file extensions.
    /// </summary>
    public string[]? BlockedExtensions { get; set; }

    /// <summary>
    /// Gets or sets whether to block executable files. Default is true.
    /// </summary>
    public bool BlockExecutables { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable ZIP bomb detection. Default is false.
    /// </summary>
    public bool EnableZipSafetyChecks { get; set; }

    /// <summary>
    /// Gets or sets the maximum nesting depth for ZIP archives. Default is 5.
    /// </summary>
    public int ZipMaxDepth { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of entries allowed in a ZIP archive. Default is 1000.
    /// </summary>
    public int ZipMaxEntries { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum compression ratio (uncompressed/compressed) for ZIP archives. Default is 200.
    /// </summary>
    public double ZipMaxCompressionRatio { get; set; } = 200.0;

    /// <summary>
    /// Gets or sets whether to validate image dimensions. Default is false.
    /// </summary>
    public bool EnableImageDimensionChecks { get; set; }

    /// <summary>
    /// Gets or sets the minimum allowed image width in pixels.
    /// </summary>
    public int? MinWidth { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed image width in pixels.
    /// </summary>
    public int? MaxWidth { get; set; }

    /// <summary>
    /// Gets or sets the minimum allowed image height in pixels.
    /// </summary>
    public int? MinHeight { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed image height in pixels.
    /// </summary>
    public int? MaxHeight { get; set; }

    /// <summary>
    /// Gets or sets the file name validation and normalization policy.
    /// </summary>
    public FileNamePolicy Policy { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to compute SHA-256 hash of file content. Default is false.
    /// </summary>
    public bool EnableHashing { get; set; }

    /// <summary>
    /// Gets or sets whether to perform malware heuristic scanning. Default is false.
    /// </summary>
    public bool EnableMalwareHeuristics { get; set; }
}