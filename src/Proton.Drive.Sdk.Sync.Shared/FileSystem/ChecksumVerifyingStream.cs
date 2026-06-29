using System.Security.Cryptography;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public sealed class ChecksumVerifyingStream : WrappingStream
{
    private readonly ReadOnlyMemory<byte> _expectedSha1;
    private readonly IncrementalHash _sha1;

    /// <summary>
    /// Last byte of the chunk that has been written to the stream, but not yet included in the hash or written to the underlying stream.
    /// This is needed to verify the checksum before writing the last byte.
    /// </summary>
    private byte? _pendingByte;

    public ChecksumVerifyingStream(Stream origin, ReadOnlyMemory<byte> expectedSha1, bool expectedSha1Verified)
        : base(origin)
    {
        _expectedSha1 = expectedSha1;
        ExpectedChecksumVerified = expectedSha1Verified;

        _sha1 = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
    }

    public bool VerificationFailed { get; private set; }
    public bool ExpectedChecksumVerified { get; }

    /// <summary>
    /// The consumer of this stream expects the pending byte to be included in the length and position,
    /// even though it hasn't been written to the underlying stream yet, so we need to add it here.
    /// </summary>
    public override long Position
    {
        get => base.Position + (_pendingByte is not null ? 1 : 0);
        set => throw new NotSupportedException("Setting position is not supported");
    }

    public override bool CanSeek => false;

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException("Seeking is not supported");

    public override void SetLength(long length)
    {
        VerifyChecksumAndCommitLastByte();
        base.SetLength(length);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Write(buffer.AsSpan(offset, count));
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Write(buffer.Span);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Writes all bytes to the underlying stream except the very last one, which is held back as a pending byte.
    /// This allows checksum verification to occur before the final byte is committed, ensuring that corrupted
    /// data is detected prior to finalizing the stream. The pending byte is written in
    /// <see cref="VerifyChecksumAndCommitLastByte"/>, which is called on <see cref="Close"/>,
    /// <see cref="DisposeAsync"/>, and <see cref="SetLength"/>.
    /// </summary>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (buffer.IsEmpty)
        {
            return;
        }

        if (_pendingByte is { } pendingByte)
        {
            var pendingByteSpan = new ReadOnlySpan<byte>(ref pendingByte);
            base.Write(pendingByteSpan);
            _pendingByte = null;
        }

        _sha1.AppendData(buffer);
        base.Write(buffer[..^1]);

        _pendingByte = buffer[^1];
    }

    public override void Close()
    {
        VerifyChecksumAndCommitLastByte();
        base.Close();
    }

    public override async ValueTask DisposeAsync()
    {
        VerifyChecksumAndCommitLastByte();
        await base.DisposeAsync().ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            if (disposing)
            {
                VerifyChecksumAndCommitLastByte();
            }
        }
        finally
        {
            if (disposing)
            {
                _sha1.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private void VerifyChecksumAndCommitLastByte()
    {
        if (_pendingByte is not { } pendingByte)
        {
            return;
        }

        ThrowIfVerifiedChecksumMismatch();
        var pendingByteSpan = new ReadOnlySpan<byte>(ref pendingByte);
        base.Write(pendingByteSpan);
        _pendingByte = null;
    }

    private void ThrowIfVerifiedChecksumMismatch()
    {
        Span<byte> sha1Checksum = stackalloc byte[_sha1.HashLengthInBytes];

        _sha1.GetCurrentHash(sha1Checksum);

        if (!sha1Checksum.SequenceEqual(_expectedSha1.Span))
        {
            VerificationFailed = true;

            if (ExpectedChecksumVerified)
            {
                throw new FileSystemClientException("Checksum verification failed", FileSystemErrorCode.IntegrityFailure);
            }
        }
    }
}
