using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Jezda.Common.Files.Security;

public static class ZipSafetyChecker
{
    public sealed record ZipSafetyOptions(int MaxDepth, int MaxEntries, double MaxCompressionRatio);

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