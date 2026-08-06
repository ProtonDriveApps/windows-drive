namespace Proton.Drive.Shared;

public sealed class DefaultPeriodicTimer : IPeriodicTimer
{
    private readonly PeriodicTimer _adaptedInstance;

    public DefaultPeriodicTimer(TimeSpan timeSpan)
    {
        _adaptedInstance = new PeriodicTimer(timeSpan);
    }

    public async ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
    {
        return await _adaptedInstance.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _adaptedInstance.Dispose();
    }
}
