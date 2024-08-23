using ProtonDrive.Client.Core.Events;

namespace ProtonDrive.App.Account;

internal sealed class UserStateChangeHandler
{
    private readonly ICoreEventProvider _coreEventProvider;
    private readonly IUserService _userService;

    public UserStateChangeHandler(ICoreEventProvider coreEventProvider, IUserService userService)
    {
        _coreEventProvider = coreEventProvider;
        _userService = userService;

        _coreEventProvider.EventsReceived += OnCoreEventsReceived;
    }

    private void OnCoreEventsReceived(object? sender, CoreEvents events)
    {
        if (events.ResumeToken.IsRefreshRequired)
        {
            _userService.Refresh();
            return;
        }

        if (events.User is not null || events.Organization is not null || events.Subscription is not null || events.UsedSpace is not null)
        {
            _userService.ApplyUpdate(events.User, events.Organization, events.Subscription, events.UsedSpace);
        }
    }
}
