using System;
using System.IO;

namespace Jezda.Common.Files.Security;

public static class ExecutableDetector
{
    public static bool IsExecutable(Stream stream, string? fileName = null)
    {
        var ext = SafeExtension(fileName);
        if (ext is "exe" or "dll" or "bat" or "cmd" or "com")
            return true;

        var pos = stream.CanSeek ? stream.Position : 0;
        try
        {
            Span<byte> header = stackalloc byte[8];
            var read = stream.Read(header);
            if (read >= 4)
            {
                // PE (Windows): "MZ"
                if (header[0] == (byte)'M' && header[1] == (byte)'Z') return true;
                // ELF: 0x7F 'E' 'L' 'F'
                if (header[0] == 0x7F && header[1] == (byte)'E' && header[2] == (byte)'L' && header[3] == (byte)'F') return true;
                // Mach-O: FE ED FA CE or CE FA ED FE or CF FA ED FE
                uint sig = (uint)(header[0] << 24 | header[1] << 16 | header[2] << 8 | header[3]);
                if (sig is 0xFEEDFACE or 0xCEFAEDFE or 0xCFFAEDFE) return true;
            }
            return false;
        }
        finally
        {
            if (stream.CanSeek) stream.Seek(pos, SeekOrigin.Begin);
        }
    }

    private static string? SafeExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(ext)) return null;
        return ext.TrimStart('.').ToLowerInvariant();
    }
}