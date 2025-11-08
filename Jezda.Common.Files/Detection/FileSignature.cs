namespace Jezda.Common.Files.Detection;

public sealed class FileSignature
{
    public required string MimeType { get; init; }
    public required byte[] Signature { get; init; }
    public int Offset { get; init; } = 0;
}