using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Jezda.Common.Files.Tests;

/// <summary>A stream that reports <c>CanSeek = false</c>, like a network or pipe stream.</summary>
internal sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
{
    public override bool CanSeek => false;
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
