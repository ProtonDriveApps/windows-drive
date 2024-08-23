using ProtonDrive.Client.Cryptography;

namespace ProtonDrive.Client.Core.Events;

internal sealed class UserAddressChangeHandler
{
    private readonly ICoreEventProvider _coreEventProvider;
    private readonly IAddressKeyProvider _addressKeyProvider;

    public UserAddressChangeHandler(ICoreEventProvider coreEventProvider, IAddressKeyProvider addressKeyProvider)
    {
        _coreEventProvider = coreEventProvider;
        _addressKeyProvider = addressKeyProvider;

        _coreEventProvider.EventsReceived += OnCoreEventsReceived;
    }

    private static bool HasUserAddressChanged(CoreEvents events)
    {
        return events.ResumeToken.IsRefreshRequired || events.HasAddressChanged;
    }

    private void OnCoreEventsReceived(object? sender, CoreEvents events)
    {
        if (HasUserAddressChanged(events))
        {
            _addressKeyProvider.ClearUserAddressesCache();
        }
    }
}
