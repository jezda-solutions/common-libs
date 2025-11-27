using System;

namespace Jezda.Common.Files.Storage;

/// <summary>
/// Represents metadata about a stored file including location, size, and public URL.
/// </summary>
public class FileDescriptor
{
    /// <summary>
    /// Gets or sets the unique identifier for the file. Defaults to a new GUID with standard formatting (with dashes).
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the file name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MIME type of the file (e.g., "image/png").
    /// </summary>
    public string? MimeType { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the file path relative to the storage root path.
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the public URL for accessing the file, if available.
    /// </summary>
    public string? PublicUrl { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the file was created. Defaults to current UTC time.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}