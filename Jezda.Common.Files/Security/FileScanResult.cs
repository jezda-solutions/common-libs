namespace Jezda.Common.Files.Security;

/// <summary>
/// Represents the result of a file malware scan operation.
/// </summary>
public sealed class FileScanResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the file is clean (no threats detected).
    /// </summary>
    public bool IsClean { get; set; }

    /// <summary>
    /// Gets or sets the name of the scanning engine used.
    /// </summary>
    public string? Engine { get; set; }

    /// <summary>
    /// Gets or sets a human-readable report of the scan result.
    /// </summary>
    public string? Report { get; set; }

    /// <summary>
    /// Gets or sets an array of specific threats or findings detected during the scan.
    /// </summary>
    public string[]? Findings { get; set; }
}