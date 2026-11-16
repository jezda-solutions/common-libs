using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Files.Security;

/// <summary>
/// Read-only view of a stream (seekable when the stream is) that starts at a given position of it.
/// <see cref="System.IO.Compression.ZipArchive"/> addresses entries by absolute offset and looks
/// for the end of the archive at the real end of the stream, so it needs the content to begin at 0.
/// The view reports <c>start</c> as position 0, never touches bytes before it and leaves the
/// underlying stream open.
/// </summary>
internal sealed class OffsetReadStream : Stream
{
    private readonly Stream _inner;
    private readonly long _start;

    /// <param name="inner">The stream to view.</param>
    /// <param name="start">Position in <paramref name="inner"/> that the view reports as 0; a seekable stream is moved there. Pass 0 for a non-seekable stream.</param>
    public OffsetReadStream(Stream inner, long start)
    {
        _inner = inner;
        _start = start;
        if (inner.CanSeek) inner.Seek(start, SeekOrigin.Begin);
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

    public override int ReadByte() => _inner.ReadByte();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.ReadAsync(buffer, cancellationToken);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.ReadAsync(buffer, offset, count, cancellationToken);

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
