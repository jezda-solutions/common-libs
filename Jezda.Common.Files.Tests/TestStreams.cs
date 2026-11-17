using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Files.Tests;

/// <summary>A stream that reports <c>CanSeek = false</c>, like a network or pipe stream.</summary>
internal sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
{
    public override bool CanSeek => false;
}

/// <summary>A seekable stream that returns at most one byte per read, as buffered or network-backed streams may.</summary>
internal sealed class OneBytePerReadStream(byte[] bytes) : MemoryStream(bytes)
{
    public override int Read(System.Span<byte> buffer) => base.Read(buffer[..System.Math.Min(1, buffer.Length)]);

    public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, System.Math.Min(1, count));
}

/// <summary>A seekable stream whose async reads always complete asynchronously, like a file-backed upload.</summary>
internal sealed class YieldingStream(byte[] bytes) : MemoryStream(bytes)
{
    public override async ValueTask<int> ReadAsync(System.Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return await base.ReadAsync(buffer, cancellationToken);
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await Task.Yield();
        return await base.ReadAsync(buffer, offset, count, cancellationToken);
    }
}
