using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Jezda.Common.Files.Security;

/// <summary>
/// Provides utilities for detecting ZIP bombs and other malicious ZIP archive patterns
/// by analyzing compression ratios, nesting depth, and entry counts.
/// </summary>
public static class ZipSafetyChecker
{
    /// <summary>
    /// Represents safety constraints for ZIP archive validation.
    /// </summary>
    /// <param name="MaxDepth">Maximum allowed nesting depth of ZIP files.</param>
    /// <param name="MaxEntries">Maximum allowed number of entries in the archive.</param>
    /// <param name="MaxCompressionRatio">Maximum allowed compression ratio (uncompressed/compressed size).</param>
    public sealed record ZipSafetyOptions(int MaxDepth, int MaxEntries, double MaxCompressionRatio);

    /// <summary>
    /// Determines whether a ZIP archive meets safety criteria to prevent ZIP bomb attacks.
    /// </summary>
    /// <param name="stream">The ZIP archive stream to analyze. Position will be restored after analysis if the stream is seekable.</param>
    /// <param name="options">The safety constraints to enforce.</param>
    /// <returns>True if the archive passes all safety checks; otherwise, false.</returns>
    public static bool IsSafe(Stream stream, ZipSafetyOptions options)
    {
        // Basic heuristics: number of entries and compression ratio.
        // Depth check is best-effort via nested .zip detection in names.
        var pos = stream.CanSeek ? stream.Position : 0;
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            int entries = 0;
            long totalCompressed = 0;
            long totalUncompressed = 0;
            int maxObservedDepth = 0;

            foreach (var entry in archive.Entries)
            {
                entries++;
                totalCompressed += entry.CompressedLength;
                totalUncompressed += entry.Length;
                var depth = entry.FullName.Split('/', '\\').Count(s => s.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                if (depth > maxObservedDepth) maxObservedDepth = depth;

                if (entries > options.MaxEntries) return false;
            }

            if (maxObservedDepth > options.MaxDepth) return false;

            if (totalCompressed > 0)
            {
                var ratio = (double)totalUncompressed / totalCompressed;
                if (ratio > options.MaxCompressionRatio) return false;
            }

            return true;
        }
        catch
        {
            // If it cannot be read as zip, treat as unsafe when checks are requested.
            return false;
        }
        finally
        {
            if (stream.CanSeek) stream.Seek(pos, SeekOrigin.Begin);
        }
    }
}