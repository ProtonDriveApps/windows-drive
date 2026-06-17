using System.IO;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Account;
using ProtonDrive.App.Notifications;
using ProtonDrive.App.Notifications.Offers;
using ProtonDrive.App.Windows.Configuration.Hyperlinks;
using ProtonDrive.App.Windows.Resources;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Windows.Services;

internal sealed class UpgradeStorageNotificationService : IUserStateAware, IOffersAware, IAccountSwitchingAware
{
    private const string NotificationGroupId = "UpgradeStorage";
    private const string NotificationId = "UpgradeStorage";
    private const string AddStorageActionName = "AddStorage";
    private const string LogoImageRelativePath = "Resources/Notifications/UpgradePlan/red-storage-logo.png";

    private static readonly TimeSpan MinIntervalBetweenNotifications = TimeSpan.FromDays(1);

    private readonly AppConfig _appConfig;
    private readonly INotificationService _notificationService;
    private readonly IExternalHyperlinks _hyperlinks;
    private readonly IClock _clock;
    private readonly IUpgradeStoragePlanAvailabilityVerifier _planAvailabilityVerifier;
    private readonly IForkingSessionUrlOpener _urlOpener;
    private readonly CoalescingAction _stateChangeHandler;

    private DateTime? _lastNotificationTimeUtc;
    private bool _notificationIsActive;
    private volatile UserState _userState = UserState.Empty;
    private volatile Offer? _activeOffer;

    public UpgradeStorageNotificationService(
        AppConfig appConfig,
        INotificationService notificationService,
        IExternalHyperlinks hyperlinks,
        IClock clock,
        IUpgradeStoragePlanAvailabilityVerifier planAvailabilityVerifier,
        IForkingSessionUrlOpener urlOpener,
        ILogger<UpgradeStorageNotificationService> logger)
    {
        _appConfig = appConfig;
        _notificationService = notificationService;
        _hyperlinks = hyperlinks;
        _clock = clock;
        _planAvailabilityVerifier = planAvailabilityVerifier;
        _urlOpener = urlOpener;

        _stateChangeHandler = logger.GetCoalescingActionWithExceptionsLoggingAndCancellationHandling(
            HandleStateChangeAsync,
            nameof(UpgradeStorageNotificationService));

        _notificationService.NotificationActivated += OnNotificationActivated;
    }

    void IUserStateAware.OnUserStateChanged(UserState value)
    {
        _userState = value;
        _stateChangeHandler.Cancel();
        _stateChangeHandler.Run();
    }

    void IOffersAware.OnActiveOfferChanged(Offer? offer)
    {
        _activeOffer = offer;
    }

    void IAccountSwitchingAware.OnAccountSwitched()
    {
        _lastNotificationTimeUtc = null;
    }

    internal Task WaitForCompletionAsync()
    {
        return _stateChangeHandler.WaitForCompletionAsync();
    }

    private async Task HandleStateChangeAsync(CancellationToken cancellationToken)
    {
        var userState = _userState;

        if (!_planAvailabilityVerifier.UpgradedPlanIsAvailable(UpgradeStoragePlanMode.UpgradeStorageNotification, userState.SubscriptionPlanCode)
            || userState.UserQuotaStatus is not (UserQuotaStatus.LimitExceeded or UserQuotaStatus.WarningLevel2Exceeded))
        {
            // The last showing time is kept, so that the quota fluctuating back above the
            // threshold within the same day does not show the notification again.
            RemoveNotification();
            return;
        }

        await Task.Delay(_appConfig.DelayBeforeShowingNotification, cancellationToken).ConfigureAwait(false);

        ShowNotificationIfNotShownRecently();
    }

    private void ShowNotificationIfNotShownRecently()
    {
        if (HasNotificationBeenShownWithin(MinIntervalBetweenNotifications))
        {
            return;
        }

        ShowNotification();

        MarkNotificationAsShown();
    }

    private void ShowNotification()
    {
        Interlocked.Exchange(ref _notificationIsActive, true);

        var notification = new Notification()
            .SetGroup(NotificationGroupId)
            .SetId(NotificationId)
            .SetHeaderText(Strings.Notification_Storage_HeaderText)
            .SetText(Strings.Notification_Storage_Message)
            .SetLogoImage(Path.Combine(_appConfig.AppFolderPath, LogoImageRelativePath))
            .AddButton(Strings.Notification_Storage_Button_AddStorage, AddStorageActionName);

        _notificationService.ShowNotification(notification);
    }

    private void RemoveNotification()
    {
        if (!Interlocked.Exchange(ref _notificationIsActive, false))
        {
            return;
        }

        _notificationService.RemoveNotificationGroup(NotificationGroupId);
    }

    private void OnNotificationActivated(object? sender, NotificationActivatedEventArgs e)
    {
        if (e.GroupId != NotificationGroupId)
        {
            return;
        }

        var offer = _activeOffer;

        if (offer?.AccountAppUrl is null)
        {
            _hyperlinks.UpgradePlanFromSidebar.Open();
        }
        else
        {
            _ = _urlOpener.TryOpenUrlAsync(offer.AccountAppUrl, "web-account-lite", CancellationToken.None);
        }
    }

    private bool HasNotificationBeenShownWithin(TimeSpan interval)
    {
        return _lastNotificationTimeUtc is not null && _lastNotificationTimeUtc.Value + interval > _clock.UtcNow;
    }

    private void MarkNotificationAsShown()
    {
        _lastNotificationTimeUtc = _clock.UtcNow;
    }
}
