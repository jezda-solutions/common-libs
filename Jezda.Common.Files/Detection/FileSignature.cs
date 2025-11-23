namespace Jezda.Common.Files.Detection;

/// <summary>
/// Represents a file signature (magic number) pattern used for MIME type detection.
/// </summary>
public sealed class FileSignature
{
    /// <summary>
    /// Gets or initializes the MIME type associated with this signature (e.g., "image/png").
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// Gets or initializes the byte sequence that identifies this file type.
    /// </summary>
    public required byte[] Signature { get; init; }

    /// <summary>
    /// Gets or initializes the byte offset where the signature appears in the file.
    /// Default is 0 (start of file).
    /// </summary>
    public int Offset { get; init; } = 0;
}