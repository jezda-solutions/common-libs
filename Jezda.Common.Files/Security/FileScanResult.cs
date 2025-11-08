namespace Jezda.Common.Files.Security;

public sealed class FileScanResult
{
    public bool IsClean { get; set; }
    public string? Engine { get; set; }
    public string? Report { get; set; }
    public string[]? Findings { get; set; }
}