using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.IO;

public sealed class ProgressReportingStream : WrappingStream
{
    private readonly Action<Progress> _progressCallback;

    public ProgressReportingStream(Stream origin, Action<Progress> progressCallback)
        : base(origin)
    {
        _progressCallback = progressCallback;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var result = base.WriteAsync(buffer, cancellationToken);

        _progressCallback.Invoke(new Progress(Position, Length));

        return result;
    }
}
