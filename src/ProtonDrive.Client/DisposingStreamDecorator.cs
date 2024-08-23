using System;
using System.IO;
using System.Threading.Tasks;
using ProtonDrive.Shared.IO;

namespace ProtonDrive.Client;

internal sealed class DisposingStreamDecorator : WrappingStream
{
    private readonly IDisposable _disposable;

    public DisposingStreamDecorator(Stream instanceToDecorate, IDisposable disposable)
    : base(instanceToDecorate)
    {
        _disposable = disposable;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _disposable.Dispose();
        }
    }

    public async override ValueTask DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
