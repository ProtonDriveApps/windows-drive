using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Update.Updates;

/// <summary>
/// Marshals app update state changed notifications of <see cref="INotifyingAppUpdate"/>
/// to synchronization context captured during creation of the object.
/// </summary>
public class SynchronizedAppUpdateDecorator : INotifyingAppUpdate
{
    private readonly INotifyingAppUpdate _origin;
    private readonly SynchronizationContext _syncContext;

    public SynchronizedAppUpdateDecorator(INotifyingAppUpdate origin)
    {
        _origin = origin;

        _syncContext = SynchronizationContext.Current!;
        _origin.StateChanged += AppUpdateOnStateChanged;
    }

    public event EventHandler<IAppUpdateState>? StateChanged;

    public void StartCheckingForUpdate(bool earlyAccess, bool manual) => _origin.StartCheckingForUpdate(earlyAccess, manual);

    public void StartUpdating(bool auto) => _origin.StartUpdating(auto);

    public Task<bool> TryInstallDownloadedUpdateAsync() => _origin.TryInstallDownloadedUpdateAsync();

    private void AppUpdateOnStateChanged(object? sender, IAppUpdateState state)
    {
        _syncContext.Post(_ => StateChanged?.Invoke(sender, state), null);
    }
}
