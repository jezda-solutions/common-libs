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
/// Falls back to text/plain detection for ASCII-heavy content.
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

    /// <inheritdoc />
    public async Task<string?> DetectAsync(Stream stream, CancellationToken ct = default)
    {
        if (!stream.CanRead) return null;

        // Large enough for every signature and for the 512-byte plain-text probe below.
        var maxLen = Signatures.Max(s => s.Signature.Length + s.Offset);
        var buffer = new byte[Math.Max(TextProbeLength, maxLen)];

        var pos = stream.CanSeek ? stream.Position : 0;
        try
        {
            // A single ReadAsync may return fewer bytes than asked; fill the signature buffer or hit EOF.
            var read = await stream.ReadAtLeastAsync(buffer.AsMemory(0, buffer.Length), buffer.Length, throwOnEndOfStream: false, ct);
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
        }
    }

    /// <summary>
    /// Plain-text heuristic over the probe: no null bytes, and more than 80% of the bytes belong to
    /// printable UTF-8 characters (ASCII or multi-byte, e.g. č, ž, Cyrillic) or tab/LF/CR.
    /// A multi-byte character cut off at the end of the probe is left out of the count.
    /// </summary>
    private static bool LooksLikeText(ReadOnlySpan<byte> probe)
    {
        var textBytes = 0;
        var examined = 0;
        while (examined < probe.Length)
        {
            var rest = probe[examined..];
            if (rest[0] == 0) return false;
            var status = Rune.DecodeFromUtf8(rest, out var rune, out var consumed);
            if (status == OperationStatus.NeedMoreData) break;
            if (status == OperationStatus.Done && (!Rune.IsControl(rune) || rune.Value is '\t' or '\n' or '\r'))
                textBytes += consumed;
            examined += consumed;
        }
        return examined > 0 && textBytes > examined * 0.8;
    }
}