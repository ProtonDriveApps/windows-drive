using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.IO;

public abstract class MappingExceptionsStream : WrappingStream
{
    private bool _faulty;

    protected MappingExceptionsStream(Stream origin)
        : base(origin)
    {
    }

    public override long Length
    {
        get
        {
            ThrowIfFaulty();

            try
            {
                return base.Length;
            }
            catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
            {
                throw mappedException;
            }
        }
    }

    public override long Position
    {
        get
        {
            ThrowIfFaulty();

            try
            {
                return base.Position;
            }
            catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
            {
                throw mappedException;
            }
        }
        set
        {
            ThrowIfFaulty();

            try
            {
                base.Position = value;
            }
            catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
            {
                throw mappedException;
            }
        }
    }

    public override int Read(byte[] bytes, int offset, int count)
    {
        ThrowIfFaulty();

        try
        {
            return base.Read(bytes, offset, count);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override int Read(Span<byte> buffer)
    {
        ThrowIfFaulty();

        try
        {
            return base.Read(buffer);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ThrowIfFaulty();

        try
        {
            return await base.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfFaulty();

        try
        {
            return await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override int ReadByte()
    {
        ThrowIfFaulty();

        try
        {
            return base.ReadByte();
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        ThrowIfFaulty();

        try
        {
            return base.BeginRead(buffer, offset, count, callback, state);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        ThrowIfFaulty();

        try
        {
            return base.EndRead(asyncResult);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        ThrowIfFaulty();

        try
        {
            return base.Seek(offset, origin);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override void SetLength(long length)
    {
        ThrowIfFaulty();

        try
        {
            base.SetLength(length);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override void Write(byte[] bytes, int offset, int count)
    {
        ThrowIfFaulty();

        try
        {
            base.Write(bytes, offset, count);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ThrowIfFaulty();

        try
        {
            base.Write(buffer);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ThrowIfFaulty();

        try
        {
            await base.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        ThrowIfFaulty();

        try
        {
            await base.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override void WriteByte(byte b)
    {
        ThrowIfFaulty();

        try
        {
            base.WriteByte(b);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        ThrowIfFaulty();

        try
        {
            return base.BeginWrite(buffer, offset, count, callback, state);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override void EndWrite(IAsyncResult asyncResult)
    {
        ThrowIfFaulty();

        try
        {
            base.EndWrite(asyncResult);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        ThrowIfFaulty();

        try
        {
            base.CopyTo(destination, bufferSize);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        ThrowIfFaulty();

        try
        {
            await base.CopyToAsync(destination, bufferSize, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override void Flush()
    {
        ThrowIfFaulty();

        try
        {
            base.Flush();
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        ThrowIfFaulty();

        try
        {
            await base.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override void Close()
    {
        // Closing or disposing the stream should not throw exceptions when faulty
        try
        {
            base.Close();
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        // Closing or disposing the stream should not throw exceptions when faulty
        try
        {
            await base.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    protected abstract bool TryMapException(Exception exception, [MaybeNullWhen(false)] out Exception mappedException);

    protected override void Dispose(bool disposing)
    {
        // Closing or disposing the stream should not throw exceptions when faulty.
        try
        {
            base.Dispose(disposing);
        }
        catch (Exception ex) when (MarkAsFaultyAndTryMapException(ex, out var mappedException))
        {
            throw mappedException;
        }
    }

    private bool MarkAsFaultyAndTryMapException(Exception exception, [MaybeNullWhen(false)] out Exception mappedException)
    {
        _faulty = true;

        return TryMapException(exception, out mappedException);
    }

    private void ThrowIfFaulty()
    {
        if (_faulty)
        {
            throw new InvalidOperationException("Stream is in a faulty state, only disposing is allowed");
        }
    }
}
