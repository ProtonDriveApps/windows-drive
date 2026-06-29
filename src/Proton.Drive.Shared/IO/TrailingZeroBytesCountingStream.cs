namespace Proton.Drive.Shared.IO;

/// <summary>
/// A stream decorator that tracks the number of consecutive zero bytes (0x00)
/// at the end of the data read from the stream.
/// </summary>
public sealed class TrailingZeroBytesCountingStream : WrappingStream
{
    public TrailingZeroBytesCountingStream(Stream origin)
        : base(origin)
    {
    }

    /// <summary>
    /// The number of consecutive zero bytes (0x00) at the end of the data read so far.
    /// </summary>
    public long TrailingZeroBytesLength { get; private set; }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override int Read(Span<byte> buffer)
    {
        throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var bytesRead = await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        TrackTrailingZeroBytes(buffer.Span[..bytesRead]);
        return bytesRead;
    }

    public override int ReadByte()
    {
        throw new NotSupportedException();
    }

    private void TrackTrailingZeroBytes(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        // Scan backwards from the end to find trailing zeros
        var trailingZeros = 0;
        for (var i = buffer.Length - 1; i >= 0; i--)
        {
            if (buffer[i] == 0)
            {
                trailingZeros++;
            }
            else
            {
                // Found a non-zero byte, stop counting and reset
                TrailingZeroBytesLength = trailingZeros;
                return;
            }
        }

        // All bytes in this buffer are zeros, add to the running count
        TrailingZeroBytesLength += trailingZeros;
    }
}
