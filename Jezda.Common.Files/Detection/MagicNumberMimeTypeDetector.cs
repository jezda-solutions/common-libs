using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Files.Detection;

public sealed class MagicNumberMimeTypeDetector : IMimeTypeDetector
{
    private static readonly FileSignature[] Signatures =
    [
        new() { MimeType = "image/png",  Signature = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } },
        new() { MimeType = "image/jpeg", Signature = new byte[] { 0xFF, 0xD8, 0xFF } },
        new() { MimeType = "application/pdf", Signature = Encoding.ASCII.GetBytes("%PDF") },
        new() { MimeType = "application/zip", Signature = new byte[] { 0x50, 0x4B, 0x03, 0x04 } },
        new() { MimeType = "application/zip", Signature = new byte[] { 0x50, 0x4B, 0x05, 0x06 } },
        new() { MimeType = "application/zip", Signature = new byte[] { 0x50, 0x4B, 0x07, 0x08 } },
        new() { MimeType = "image/gif",  Signature = Encoding.ASCII.GetBytes("GIF87a") },
        new() { MimeType = "image/gif",  Signature = Encoding.ASCII.GetBytes("GIF89a") },
        new() { MimeType = "image/webp", Signature = Encoding.ASCII.GetBytes("RIFF") }, // requires "WEBP" at offset 8, handled below
    ];

    public async Task<string?> DetectAsync(Stream stream, CancellationToken ct = default)
    {
        if (!stream.CanRead) return null;

        var maxLen = Signatures.Max(s => s.Signature.Length + s.Offset);
        var buffer = new byte[Math.Max(16, maxLen)];

        var pos = stream.CanSeek ? stream.Position : 0;
        try
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            if (read <= 0) return null;

            foreach (var sig in Signatures)
            {
                if (read < sig.Signature.Length + sig.Offset) continue;
                var match = true;
                for (int i = 0; i < sig.Signature.Length; i++)
                {
                    if (buffer[sig.Offset + i] != sig.Signature[i]) { match = false; break; }
                }
                if (match)
                {
                    // Special handling for WEBP (RIFF...WEBP at offset 8)
                    if (sig.MimeType == "image/webp" && read >= 12)
                    {
                        var fourCC = Encoding.ASCII.GetString(buffer, 8, 4);
                        if (!string.Equals(fourCC, "WEBP", StringComparison.Ordinal))
                            continue;
                    }
                    // If ZIP matched, try to refine to Office OOXML types when possible
                    if (sig.MimeType == "application/zip")
                    {
                        if (stream.CanSeek)
                        {
                            var current = stream.Position;
                            try
                            {
                                stream.Seek(0, SeekOrigin.Begin);
                                using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
                                var hasWord = archive.Entries.Any(e => e.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase));
                                var hasXl = archive.Entries.Any(e => e.FullName.StartsWith("xl/", StringComparison.OrdinalIgnoreCase));
                                var hasPpt = archive.Entries.Any(e => e.FullName.StartsWith("ppt/", StringComparison.OrdinalIgnoreCase));
                                if (hasWord)
                                    return "application/vnd.openxmlformats-officedocument.wordprocessingml.document"; // .docx
                                if (hasXl)
                                    return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"; // .xlsx
                                if (hasPpt)
                                    return "application/vnd.openxmlformats-officedocument.presentationml.presentation"; // .pptx
                            }
                            catch
                            {
                                // Fall back to generic zip if inspection fails
                            }
                            finally
                            {
                                stream.Seek(current, SeekOrigin.Begin);
                            }
                        }
                        return "application/zip";
                    }
                    return sig.MimeType;
                }
            }

            // Plain text heuristic: ASCII heavy and no null bytes in first 512 bytes
            var textProbeLen = Math.Min(read, 512);
            var nullFound = false;
            var asciiLikely = 0;
            for (int i = 0; i < textProbeLen; i++)
            {
                var b = buffer[i];
                if (b == 0) { nullFound = true; break; }
                if (b == 0x09 || b == 0x0A || b == 0x0D || (b >= 0x20 && b <= 0x7E)) asciiLikely++;
            }
            if (!nullFound && asciiLikely > textProbeLen * 0.8)
                return "text/plain";

            return null;
        }
        finally
        {
            if (stream.CanSeek) stream.Seek(pos, SeekOrigin.Begin);
        }
    }
}