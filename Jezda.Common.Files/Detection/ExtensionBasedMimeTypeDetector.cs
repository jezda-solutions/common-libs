using System;
using System.Collections.Generic;
using System.IO;

namespace Jezda.Common.Files.Detection;

/// <summary>
/// Detects MIME types based on file extensions. This is a fast, lightweight detector
/// that should be used as a fallback when magic number detection is not available or fails.
/// </summary>
public static class ExtensionBasedMimeTypeDetector
{
    private static readonly Dictionary<string, string> ExtensionToMimeType = new(StringComparer.OrdinalIgnoreCase)
    {
        // Text
        { ".txt", "text/plain" },
        { ".html", "text/html" },
        { ".htm", "text/html" },
        { ".css", "text/css" },
        { ".js", "application/javascript" },
        { ".json", "application/json" },
        { ".xml", "application/xml" },
        { ".csv", "text/csv" },

        // Images
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".gif", "image/gif" },
        { ".bmp", "image/bmp" },
        { ".webp", "image/webp" },
        { ".svg", "image/svg+xml" },
        { ".ico", "image/x-icon" },
        { ".tiff", "image/tiff" },
        { ".tif", "image/tiff" },

        // Documents
        { ".pdf", "application/pdf" },
        { ".doc", "application/msword" },
        { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
        { ".xls", "application/vnd.ms-excel" },
        { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
        { ".ppt", "application/vnd.ms-powerpoint" },
        { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
        { ".odt", "application/vnd.oasis.opendocument.text" },
        { ".ods", "application/vnd.oasis.opendocument.spreadsheet" },
        { ".odp", "application/vnd.oasis.opendocument.presentation" },

        // Archives
        { ".zip", "application/zip" },
        { ".rar", "application/x-rar-compressed" },
        { ".7z", "application/x-7z-compressed" },
        { ".tar", "application/x-tar" },
        { ".gz", "application/gzip" },

        // Audio
        { ".mp3", "audio/mpeg" },
        { ".wav", "audio/wav" },
        { ".ogg", "audio/ogg" },
        { ".m4a", "audio/mp4" },
        { ".flac", "audio/flac" },

        // Video
        { ".mp4", "video/mp4" },
        { ".avi", "video/x-msvideo" },
        { ".mov", "video/quicktime" },
        { ".wmv", "video/x-ms-wmv" },
        { ".flv", "video/x-flv" },
        { ".webm", "video/webm" },
        { ".mkv", "video/x-matroska" },
    };

    /// <summary>
    /// Detects MIME type from file name extension.
    /// </summary>
    /// <param name="fileName">The file name with extension.</param>
    /// <returns>The detected MIME type, or "application/octet-stream" if unknown.</returns>
    public static string Detect(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "application/octet-stream";

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension))
            return "application/octet-stream";

        return ExtensionToMimeType.TryGetValue(extension, out var mimeType)
            ? mimeType
            : "application/octet-stream";
    }

    /// <summary>
    /// Tries to detect MIME type from file name extension.
    /// </summary>
    /// <param name="fileName">The file name with extension.</param>
    /// <param name="mimeType">The detected MIME type, if successful.</param>
    /// <returns>True if a known MIME type was found; otherwise false.</returns>
    public static bool TryDetect(string fileName, out string mimeType)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            mimeType = string.Empty;
            return false;
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension))
        {
            mimeType = string.Empty;
            return false;
        }

        return ExtensionToMimeType.TryGetValue(extension, out mimeType!);
    }
}
