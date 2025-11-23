using System;
using System.IO;

namespace Jezda.Common.Files.Security;

/// <summary>
/// Provides utilities for validating image file dimensions to prevent decompression bombs
/// and enforce size constraints. Supports PNG and JPEG formats.
/// </summary>
public static class ImageSafetyChecker
{
    /// <summary>
    /// Represents dimension boundaries for image validation.
    /// </summary>
    /// <param name="MinWidth">Minimum allowed image width in pixels.</param>
    /// <param name="MaxWidth">Maximum allowed image width in pixels.</param>
    /// <param name="MinHeight">Minimum allowed image height in pixels.</param>
    /// <param name="MaxHeight">Maximum allowed image height in pixels.</param>
    public sealed record ImageBounds(int? MinWidth, int? MaxWidth, int? MinHeight, int? MaxHeight);

    /// <summary>
    /// Validates that image dimensions fall within the specified bounds.
    /// </summary>
    /// <param name="stream">The image stream to analyze. Position will be restored after analysis if the stream is seekable.</param>
    /// <param name="mimeType">The MIME type of the image (e.g., "image/png", "image/jpeg").</param>
    /// <param name="bounds">The dimension constraints to enforce.</param>
    /// <returns>True if the image dimensions are within bounds or the format is not supported; otherwise, false.</returns>
    public static bool ValidateDimensions(Stream stream, string mimeType, ImageBounds bounds)
    {
        var pos = stream.CanSeek ? stream.Position : 0;
        try
        {
            (int width, int height)? dims = mimeType switch
            {
                "image/png" => ReadPngDimensions(stream),
                "image/jpeg" => ReadJpegDimensions(stream),
                _ => null
            };
            if (dims is null) return true; // Non-supported types pass

            var (w, h) = dims.Value;
            if (bounds.MinWidth.HasValue && w < bounds.MinWidth.Value) return false;
            if (bounds.MaxWidth.HasValue && w > bounds.MaxWidth.Value) return false;
            if (bounds.MinHeight.HasValue && h < bounds.MinHeight.Value) return false;
            if (bounds.MaxHeight.HasValue && h > bounds.MaxHeight.Value) return false;
            return true;
        }
        finally
        {
            if (stream.CanSeek) stream.Seek(pos, SeekOrigin.Begin);
        }
    }

    private static (int width, int height)? ReadPngDimensions(Stream stream)
    {
        Span<byte> header = stackalloc byte[24]; // PNG sig (8) + IHDR (length+type 8) + width/height (8)
        var read = stream.Read(header);
        if (read < 24) return null;
        // Validate PNG signature
        if (!(header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A))
            return null;
        // IHDR should start at byte 8; width and height are at offset 16 and 20
        int width = ReadBigEndianInt(header[16..20]);
        int height = ReadBigEndianInt(header[20..24]);
        return (width, height);
    }

    private static (int width, int height)? ReadJpegDimensions(Stream stream)
    {
        // Minimal JPEG SOF parsing
        if (stream.ReadByte() != 0xFF || stream.ReadByte() != 0xD8) return null; // SOI
        while (true)
        {
            int markerStart = stream.ReadByte();
            if (markerStart == -1) return null;
            if (markerStart != 0xFF) continue;
            int marker = stream.ReadByte();
            if (marker == -1) return null;
            // Skip padding FFs
            while (marker == 0xFF) marker = stream.ReadByte();

            // Read segment length
            int lh = stream.ReadByte();
            int ll = stream.ReadByte();
            if (lh == -1 || ll == -1) return null;
            int len = (lh << 8) + ll;
            if (len < 2) return null;

            // SOF0/1/2 markers define dimensions
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                // Precision(1) + Height(2) + Width(2) + components...
                stream.ReadByte();
                int h = (stream.ReadByte() << 8) + stream.ReadByte();
                int w = (stream.ReadByte() << 8) + stream.ReadByte();
                return (w, h);
            }
            else
            {
                // Skip segment payload
                stream.Seek(len - 2, SeekOrigin.Current);
            }
        }
    }

    private static int ReadBigEndianInt(ReadOnlySpan<byte> bytes)
    {
        return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
    }
}