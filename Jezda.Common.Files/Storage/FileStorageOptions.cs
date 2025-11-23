using System;

namespace Jezda.Common.Files.Storage;

/// <summary>
/// Configuration options for file storage behavior.
/// </summary>
public class FileStorageOptions
{
    /// <summary>
    /// Gets or sets the absolute root path on the server where files are stored.
    /// Configure differently for development and production environments.
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional public URL prefix for accessing files via web server or CDN.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// Gets or sets whether to organize files in date-based folders (yyyy/MM/dd) inside the sub-path.
    /// Default is true.
    /// </summary>
    public bool UseDateFolders { get; set; } = true;
}