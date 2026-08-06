namespace Proton.Drive.Shared.IO;

/// <summary>
/// A write-only stream wrapper that reports progress during write operations with configurable granularity.
/// Does not support reading or seeking operations.
/// </summary>
public sealed class WriteOnlyProgressReportingStream : WrappingStream
{
    private const double MinimumReportingThreshold = 0.01; // Only report every 1% change to avoid unnecessary updates

    private readonly Action<Progress> _progressCallback;

    private Progress _lastReportedProgress = Progress.Zero;

    public WriteOnlyProgressReportingStream(Stream origin, Action<Progress> progressCallback)
        : base(origin)
    {
        _progressCallback = progressCallback;
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        base.Write(buffer);

        UpdateAndReportProgressIfSignificant();
    }

    public override void Write(byte[] bytes, int offset, int count)
    {
        base.Write(bytes, offset, count);

        UpdateAndReportProgressIfSignificant();
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await base.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);

        UpdateAndReportProgressIfSignificant();
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await base.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);

        UpdateAndReportProgressIfSignificant();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException("Reading is not supported. This stream is write-only.");
    }

    public override int Read(Span<byte> buffer)
    {
        throw new NotSupportedException("Reading is not supported. This stream is write-only.");
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Reading is not supported. This stream is write-only.");
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Reading is not supported. This stream is write-only.");
    }

    public override int ReadByte()
    {
        throw new NotSupportedException("Reading is not supported. This stream is write-only.");
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException("Seeking is not supported. This stream is write-only.");
    }

    private void UpdateAndReportProgressIfSignificant()
    {
        var progress = new Progress(Position, Length);

        if (!HasSignificantChange(progress))
        {
            return;
        }

        _progressCallback.Invoke(progress);
        _lastReportedProgress = progress;
    }

    private bool HasSignificantChange(Progress progress)
    {
        return _lastReportedProgress.Ratio == 0 ||
            progress.Ratio > _lastReportedProgress.Ratio + MinimumReportingThreshold ||
            (progress.Ratio >= 1 && _lastReportedProgress.Ratio < 1.0);
    }
}
