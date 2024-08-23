using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.IO;

public abstract class WrappingStream : Stream
{
    protected WrappingStream(Stream origin)
    {
        Origin = origin;
    }

    public override bool CanRead => Origin.CanRead;
    public override bool CanWrite => Origin.CanWrite;
    public override bool CanSeek => Origin.CanSeek;
    public override bool CanTimeout => Origin.CanTimeout;
    public override long Length => Origin.Length;

    public override long Position
    {
        get => Origin.Position;
        set => Origin.Position = value;
    }

    public override int ReadTimeout
    {
        get => Origin.ReadTimeout;
        set => Origin.ReadTimeout = value;
    }

    public override int WriteTimeout
    {
        get => Origin.WriteTimeout;
        set => Origin.WriteTimeout = value;
    }

    protected Stream Origin { get; }

    public override int Read(byte[] bytes, int offset, int count)
    {
        return Origin.Read(bytes, offset, count);
    }

    public override int Read(Span<byte> buffer)
    {
        return Origin.Read(buffer);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return Origin.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return Origin.ReadAsync(buffer, cancellationToken);
    }

    public override int ReadByte()
    {
        return Origin.ReadByte();
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        return Origin.BeginRead(buffer, offset, count, callback, state);
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        return Origin.EndRead(asyncResult);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return Origin.Seek(offset, origin);
    }

    public override void SetLength(long length)
    {
        Origin.SetLength(length);
    }

    public override void Write(byte[] bytes, int offset, int count)
    {
        Origin.Write(bytes, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Origin.Write(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return Origin.WriteAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return Origin.WriteAsync(buffer, cancellationToken);
    }

    public override void WriteByte(byte b)
    {
        Origin.WriteByte(b);
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        return Origin.BeginWrite(buffer, offset, count, callback, state);
    }

    public override void EndWrite(IAsyncResult asyncResult)
    {
        Origin.EndWrite(asyncResult);
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        Origin.CopyTo(destination, bufferSize);
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        return Origin.CopyToAsync(destination, bufferSize, cancellationToken);
    }

    public override void Flush()
    {
        Origin.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return Origin.FlushAsync(cancellationToken);
    }

    public override void Close()
    {
        // On the off chance that some wrapped stream has different
        // semantics for Close vs. Dispose, let's preserve that.
        try
        {
            Origin.Close();
        }
        finally
        {
            base.Close();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await Origin.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                // Explicitly pick up a method implementing Dispose
                ((IDisposable)Origin).Dispose();
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }
}
