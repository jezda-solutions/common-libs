using System;

namespace Jezda.Common.Files.Storage;

public class FileStorageOptions
{
    // Absolute root path on the server (configure differently for dev/prod)
    public string RootPath { get; set; } = string.Empty;

    // Optional public URL prefix (served via web server/CDN)
    public string? PublicBaseUrl { get; set; }

    // Organize files by date (yyyy/MM/dd) inside the subPath
    public bool UseDateFolders { get; set; } = true;
}