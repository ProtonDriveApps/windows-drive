using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared;

public sealed class DefaultPeriodicTimer : IPeriodicTimer
{
    private readonly PeriodicTimer _adaptedInstance;

    public DefaultPeriodicTimer(TimeSpan timeSpan)
    {
        _adaptedInstance = new PeriodicTimer(timeSpan);
    }

    public async ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
    {
        return await _adaptedInstance.WaitForNextTickAsync(cancellationToken);
    }

    public void Dispose()
    {
        _adaptedInstance.Dispose();
    }
}
