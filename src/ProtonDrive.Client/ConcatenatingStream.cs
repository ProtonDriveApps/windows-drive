using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Client;

internal sealed class ConcatenatingStream : Stream
{
    private readonly Stream _stream1;
    private readonly Stream _stream2;
    private long _position;
    private bool _stream1EntirelyRead;

    public ConcatenatingStream(Stream stream1, Stream stream2)
    {
        _stream1 = stream1;
        _stream2 = stream2;
    }

    public override bool CanRead => true;
    public override bool CanSeek => _stream1.CanSeek && _stream2.CanSeek;
    public override bool CanWrite => false;
    public override long Length => CanSeek ? _stream1.Length + _stream2.Length : throw new NotSupportedException();

    public override long Position
    {
        get => CanSeek ? _position : throw new NotSupportedException();
        set => _position = CanSeek ? value : throw new NotSupportedException();
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var readTask = ReadAsync(buffer, offset, count);
        return readTask.GetAwaiter().GetResult();
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        EnsurePositionInUnderlyingStreamsIfPossible();

        return FinishReadAsync();

        // Avoiding 'async/await' state machine in the current method so that argument exceptions can be caught before the task is returned to the caller.
        async ValueTask<int> FinishReadAsync()
        {
            int readCount = 0;

            if (!_stream1EntirelyRead)
            {
                readCount = await _stream1.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }

            if (readCount < buffer.Length)
            {
                _stream1EntirelyRead = true;
                readCount += await _stream2.ReadAsync(buffer[readCount..], cancellationToken).ConfigureAwait(false);
            }

            if (CanSeek)
            {
                _position += readCount;
            }

            return readCount;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (!CanSeek)
        {
            throw new NotSupportedException();
        }

        if (offset > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new InvalidEnumArgumentException(nameof(origin), (int)origin, typeof(SeekOrigin)),
        };

        ApplyPositionToUnderlyingStreams(newPosition, (stream, position) => stream.Seek(position, SeekOrigin.Begin));

        return _position = newPosition;
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        EnsurePositionInUnderlyingStreamsIfPossible();

        var stream1CopyTask = _stream1.CopyToAsync(destination, bufferSize, cancellationToken);

        // Avoiding 'async/await' state machine in the current method so that argument exceptions can be caught before the task is returned to the caller.
        async Task FinishCopyToAsync()
        {
            await stream1CopyTask.ConfigureAwait(false);
            await _stream2.CopyToAsync(destination, bufferSize, cancellationToken).ConfigureAwait(false);

            if (CanSeek)
            {
                _position = _stream1.Length + _stream2.Length;
            }
        }

        return FinishCopyToAsync();
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        EnsurePositionInUnderlyingStreamsIfPossible();

        _stream1.CopyTo(destination, bufferSize);
        _stream2.CopyTo(destination, bufferSize);

        if (CanSeek)
        {
            _position = _stream1.Length + _stream2.Length;
        }
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        await _stream1.DisposeAsync().ConfigureAwait(false);
        await _stream2.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsync().ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream1.Dispose();
            _stream2.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ApplyPositionToUnderlyingStreams(long position, Action<Stream, long> applyPositionAction)
    {
        if (position < _stream1.Length)
        {
            applyPositionAction(_stream1, position);
            applyPositionAction(_stream2, 0);
        }
        else
        {
            applyPositionAction(_stream1, 0);
            applyPositionAction(_stream2, position - _stream1.Length);
        }
    }

    private void EnsurePositionInUnderlyingStreamsIfPossible()
    {
        if (CanSeek)
        {
            ApplyPositionToUnderlyingStreams(Position, (stream, position) => stream.Position = position);
        }
    }
}
