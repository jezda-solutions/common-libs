using System;
using System.IO;

namespace Jezda.Common.Files.Security;

/// <summary>
/// Read-only view of a stream (seekable when the stream is) that starts at the stream's current position.
/// <see cref="System.IO.Compression.ZipArchive"/> addresses entries by absolute offset and looks
/// for the end of the archive at the real end of the stream, so it needs the content to begin at 0.
/// The view reports the start position as 0, never touches bytes before it and leaves the
/// underlying stream open.
/// </summary>
internal sealed class OffsetReadStream : Stream
{
    private readonly Stream _inner;
    private readonly long _start;

    public OffsetReadStream(Stream inner)
    {
        _inner = inner;
        _start = inner.CanSeek ? inner.Position : 0;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => Math.Max(0, _inner.Length - _start);

    public override long Position
    {
        get => _inner.Position - _start;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => _inner.Read(buffer);

    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        if (target < 0) throw new IOException("An attempt was made to move the position before the beginning of the stream.");
        _inner.Seek(_start + target, SeekOrigin.Begin);
        return target;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
