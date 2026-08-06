using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Client;

internal sealed class DisposingStreamDecorator : WrappingStream
{
    private readonly IDisposable _disposable;

    public DisposingStreamDecorator(Stream instanceToDecorate, IDisposable disposable)
    : base(instanceToDecorate)
    {
        _disposable = disposable;
    }

    public async override ValueTask DisposeAsync()
    {
        await base.DisposeAsync().ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _disposable.Dispose();
        }
    }
}
