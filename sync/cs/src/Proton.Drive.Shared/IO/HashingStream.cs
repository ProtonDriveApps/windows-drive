using System.Security.Cryptography;

namespace Proton.Drive.Shared.IO;

public sealed class HashingStream : WrappingStream
{
    private readonly IncrementalHash _hash;

    public HashingStream(Stream origin, HashAlgorithmName hashAlgorithmName)
        : base(origin)
    {
        _hash = IncrementalHash.CreateHash(hashAlgorithmName);
    }

    public long NumberOfBytesHashed { get; set; }

    public override bool CanSeek => false;

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        throw new NotSupportedException();
    }

    public override void EndWrite(IAsyncResult asyncResult)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] bytes, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        throw new NotSupportedException();
    }

    public override void WriteByte(byte b)
    {
        throw new NotSupportedException();
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        _hash.AppendData(buffer, offset, count);

        NumberOfBytesHashed += count;

        return base.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _hash.AppendData(buffer.Span);

        NumberOfBytesHashed += buffer.Length;

        return base.WriteAsync(buffer, cancellationToken);
    }

    public int GetCurrentHash(Span<byte> hashDestination)
    {
        return _hash.GetCurrentHash(hashDestination);
    }

    public override ValueTask DisposeAsync()
    {
        _hash.Dispose();
        return base.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        _hash.Dispose();
        base.Dispose(disposing);
    }
}
