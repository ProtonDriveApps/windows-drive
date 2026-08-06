using System.Diagnostics;

namespace Proton.Drive.Shared.IO;

public sealed class InvertingStream : Stream
{
    private readonly SemaphoreSlim _readingSemaphore = new(0, 1);
    private readonly SemaphoreSlim _readingBufferCompletionSemaphore = new(0, 1);

    private long _length;
    private long _position;
    private ReadOnlyMemory<byte> _buffer;
    private int _bufferOffset;

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        _length = value;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush()
    {
    }

    /// <summary>
    /// Asynchronously reads a sequence of bytes and advances the position by the number of bytes read, and monitors cancellation requests.
    /// </summary>
    /// <param name="buffer">The region of memory to write the data into.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// A task that represents the asynchronous read operation. The value of its Result property contains the total number
    /// of bytes read into the buffer. The result value can be less than the length of the buffer if that many bytes are not currently available,
    /// or it can be 0 (zero) if the length of the buffer is 0 or if the end of the stream has been reached.
    /// </returns>
    public async override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty)
        {
            return 0;
        }

        await _readingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_buffer.IsEmpty)
            {
                // Writing completed
                return 0;
            }

            var numberOfBytesToCopy = Math.Min(_buffer.Length - _bufferOffset, buffer.Length);
            _buffer.Slice(_bufferOffset, numberOfBytesToCopy).CopyTo(buffer);
            _bufferOffset += numberOfBytesToCopy;
            _position += numberOfBytesToCopy;

            if (_position > _length)
            {
                _length = _position;
            }

            return numberOfBytesToCopy;
        }
        finally
        {
            if (_bufferOffset == _buffer.Length)
            {
                _readingBufferCompletionSemaphore.Release();
            }
            else
            {
                _readingSemaphore.Release();
            }
        }
    }

    /// <summary>
    /// Asynchronously writes a sequence of bytes to the current stream, advances the current position
    /// within this stream by the number of bytes written, and monitors cancellation requests.
    /// </summary>
    /// <remarks>
    /// Writing operation completes when the provided sequence of bytes is fully read.
    /// </remarks>
    /// <param name="buffer">The region of memory to write data from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public async override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Debug.Assert(_readingSemaphore.CurrentCount == 0, "The number of remaining threads that can enter a semaphore must be zero");
        Debug.Assert(_readingBufferCompletionSemaphore.CurrentCount == 0, "The number of remaining threads that can enter a semaphore must be zero");

        if (buffer.IsEmpty)
        {
            return;
        }

        _buffer = buffer;
        _bufferOffset = 0;
        _readingSemaphore.Release();

        try
        {
            await _readingBufferCompletionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await _readingSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            _buffer = default;
        }
    }

    /// <summary>
    /// Completes writing. Triggers reading operation to complete with zero bytes read.
    /// </summary>
    public void CompleteWriting()
    {
        Debug.Assert(_readingSemaphore.CurrentCount == 0, "The number of remaining threads that can enter a semaphore must be zero");
        Debug.Assert(_readingBufferCompletionSemaphore.CurrentCount == 0, "The number of remaining threads that can enter a semaphore must be zero");

        _buffer = default;
        _readingSemaphore.Release();
    }
}
