namespace Jezda.Common.Files.Validation;

/// <summary>
/// Provides per-file overrides for validation options, allowing specific files to use different
/// validation rules than the global configuration.
/// </summary>
public sealed class FileValidationOverrides
{
    /// <summary>
    /// Gets or sets the maximum allowed file size in bytes. Overrides the default if specified.
    /// </summary>
    public long? MaxSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the allowed MIME types. Overrides the default if specified.
    /// </summary>
    public string[]? AllowedMimeTypes { get; set; }

    /// <summary>
    /// Gets or sets the allowed file extensions. Overrides the default if specified.
    /// </summary>
    public string[]? AllowedExtensions { get; set; }

    /// <summary>
    /// Gets or sets the blocked file extensions. Overrides the default if specified.
    /// </summary>
    public string[]? BlockedExtensions { get; set; }

    /// <summary>
    /// Gets or sets whether to block executable files. Overrides the default if specified.
    /// </summary>
    public bool? BlockExecutables { get; set; }

    /// <summary>
    /// Gets or sets whether to enable ZIP bomb detection. Overrides the default if specified.
    /// </summary>
    public bool? EnableZipSafetyChecks { get; set; }

    /// <summary>
    /// Gets or sets the maximum nesting depth for ZIP archives. Overrides the default if specified.
    /// </summary>
    public int? ZipMaxDepth { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of entries in ZIP archives. Overrides the default if specified.
    /// </summary>
    public int? ZipMaxEntries { get; set; }

    /// <summary>
    /// Gets or sets the maximum compression ratio for ZIP archives. Overrides the default if specified.
    /// </summary>
    public double? ZipMaxCompressionRatio { get; set; }

    /// <summary>
    /// Gets or sets whether to validate image dimensions. Overrides the default if specified.
    /// </summary>
    public bool? EnableImageDimensionChecks { get; set; }

    /// <summary>
    /// Gets or sets the minimum allowed image width. Overrides the default if specified.
    /// </summary>
    public int? MinWidth { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed image width. Overrides the default if specified.
    /// </summary>
    public int? MaxWidth { get; set; }

    /// <summary>
    /// Gets or sets the minimum allowed image height. Overrides the default if specified.
    /// </summary>
    public int? MinHeight { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed image height. Overrides the default if specified.
    /// </summary>
    public int? MaxHeight { get; set; }

    /// <summary>
    /// Gets or sets whether to compute file hash. Overrides the default if specified.
    /// </summary>
    public bool? EnableHashing { get; set; }

    /// <summary>
    /// Gets or sets whether to perform malware scanning. Overrides the default if specified.
    /// </summary>
    public bool? EnableMalwareHeuristics { get; set; }
}