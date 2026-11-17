using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text;
using Jezda.Common.Files.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Files.Detection;

/// <summary>
/// Detects MIME types by analyzing file magic numbers (byte signatures).
/// Supports common formats including images (PNG, JPEG, GIF, WebP), documents (PDF),
/// archives (ZIP), and Office Open XML formats (DOCX, XLSX, PPTX).
/// Falls back to text/plain for content that looks like text in any single- or multi-byte
/// encoding (UTF-8, Windows-1250/1251, ...): no null bytes and almost no control bytes in the
/// first 512 bytes.
/// </summary>
public sealed class MagicNumberMimeTypeDetector : IMimeTypeDetector
{
    private const int TextProbeLength = 512;

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

    // Large enough for every signature and for the 512-byte plain-text probe.
    private static readonly int BufferLength = Math.Max(TextProbeLength, Signatures.Max(s => s.Signature.Length + s.Offset));

    /// <inheritdoc />
    public async Task<string?> DetectAsync(Stream stream, CancellationToken ct = default)
    {
        if (!stream.CanRead) return null;

        var buffer = ArrayPool<byte>.Shared.Rent(BufferLength);
        var pos = stream.CanSeek ? stream.Position : 0;
        try
        {
            // A single ReadAsync may return fewer bytes than asked; fill the signature buffer or hit EOF.
            var read = await stream.ReadAtLeastAsync(buffer.AsMemory(0, BufferLength), BufferLength, throwOnEndOfStream: false, ct);
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
                    if (sig.MimeType == "image/webp"
                        && (read < 12 || !string.Equals(Encoding.ASCII.GetString(buffer, 8, 4), "WEBP", StringComparison.Ordinal)))
                        continue;
                    // If ZIP matched, try to refine to Office OOXML types when possible
                    if (sig.MimeType == "application/zip")
                    {
                        // ZipArchive addresses entries by absolute offset, so inspect a view that
                        // starts at the stream's current position.
                        if (stream.CanSeek)
                        {
                            try
                            {
                                using var view = new OffsetReadStream(stream, pos);
                                using var archive = new ZipArchive(view, ZipArchiveMode.Read, leaveOpen: true);
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
                        }
                        return "application/zip";
                    }
                    return sig.MimeType;
                }
            }

            return LooksLikeText(buffer.AsSpan(0, Math.Min(read, TextProbeLength))) ? "text/plain" : null;
        }
        finally
        {
            if (stream.CanSeek) stream.Seek(pos, SeekOrigin.Begin);
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Plain-text heuristic over the probe, independent of the encoding: text has no null bytes and
    /// (almost) no control bytes other than tab, LF, CR, form feed and escape, in ASCII, UTF-8 or a
    /// legacy single-byte code page alike. Binary data is full of both, so more than 1% control
    /// bytes counts as binary; one stray control byte (e.g. a DOS Ctrl+Z at the end) is always allowed.
    /// </summary>
    private static bool LooksLikeText(ReadOnlySpan<byte> probe)
    {
        var controls = 0;
        foreach (var b in probe)
        {
            if (b == 0) return false;
            if ((b < 0x20 && b is not (0x09 or 0x0A or 0x0C or 0x0D or 0x1B)) || b == 0x7F) controls++;
        }
        return probe.Length > 0 && controls <= Math.Max(1, probe.Length / 100);
    }
}
