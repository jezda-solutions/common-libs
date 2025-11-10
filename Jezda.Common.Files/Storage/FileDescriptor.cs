using System;

namespace Jezda.Common.Files.Storage;

public class FileDescriptor
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public long Size { get; set; }
    public string RelativePath { get; set; } = string.Empty; // path relative to RootPath
    public string? PublicUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}