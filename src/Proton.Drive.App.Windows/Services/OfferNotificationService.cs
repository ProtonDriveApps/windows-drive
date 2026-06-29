using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proton.Drive.App.Notifications;
using Proton.Drive.App.Notifications.Offers;
using Proton.Drive.App.Onboarding;
using Proton.Drive.App.Settings;
using Proton.Drive.App.Windows.Views.Offer;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Features;
using Proton.Drive.Shared.Logging;
using Proton.Drive.Shared.Repository;
using Proton.Drive.Shared.Threading;
using static Proton.Drive.App.Settings.NotificationSettings;

namespace Proton.Drive.App.Windows.Services;

internal sealed class OfferNotificationService : IStoppableService, IOnboardingStateAware, IOffersAware, IFeatureFlagsAware
{
    public const string NotificationGroupId = "Offer";
    public const string NotificationId = "Offer";
    public const string GetDealActionName = "GetDeal";

    private readonly INotificationService _notificationService;
    private readonly IDialogService _dialogService;
    private readonly IApp _app;
    private readonly Func<OfferViewModel> _offerViewModelFactory;
    private readonly IRepository<NotificationSettings> _settingsRepository;
    private readonly IClock _clock;
    private readonly IScheduler _scheduler;
    private readonly ILogger<OfferNotificationService> _logger;

    private readonly CoalescingAction _stateChangeHandler;
    private readonly TimeSpan _delayBeforeShowingNotification;

    private OnboardingState _onboardingState = OnboardingState.Initial;
    private Offer? _offer;
    private bool _notificationPopupEnabled;
    private bool _isStopping;

    public OfferNotificationService(
        AppConfig appConfig,
        INotificationService notificationService,
        IDialogService dialogService,
        IApp app,
        Func<OfferViewModel> offerViewModelFactory,
        IRepository<NotificationSettings> settingsRepository,
        IClock clock,
        [FromKeyedServices("Dispatcher")] IScheduler scheduler,
        ILogger<OfferNotificationService> logger)
    {
        _notificationService = notificationService;
        _dialogService = dialogService;
        _app = app;
        _offerViewModelFactory = offerViewModelFactory;
        _settingsRepository = settingsRepository;
        _clock = clock;
        _scheduler = scheduler;
        _logger = logger;
        _delayBeforeShowingNotification = appConfig.DelayBeforeShowingNotification;

        _stateChangeHandler = logger.GetCoalescingActionWithExceptionsLoggingAndCancellationHandling(HandleExternalStateChangeAsync, nameof(OfferNotificationService));

        _notificationService.NotificationActivated += OnNotificationServiceNotificationActivated;
    }

    void IOnboardingStateAware.OnboardingStateChanged(OnboardingState value)
    {
        _onboardingState = value;

        ScheduleExternalStateChangeHandling(forceRestart: false);
    }

    void IOffersAware.OnActiveOfferChanged(Offer? offer)
    {
        _offer = offer;

        ScheduleExternalStateChangeHandling(forceRestart: true);
    }

    void IFeatureFlagsAware.OnFeatureFlagsChanged(IReadOnlyDictionary<Feature, bool> features)
    {
        _notificationPopupEnabled = features[Feature.DriveWindowsOffersSystemNotificationPopup];
    }

    async Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug($"{nameof(OfferNotificationService)} is stopping");

        _isStopping = true;
        _stateChangeHandler.Cancel();

        await WaitForCompletionAsync().ConfigureAwait(false);

        _logger.LogDebug($"{nameof(OfferNotificationService)} stopped");
    }

    internal Task WaitForCompletionAsync()
    {
        return _stateChangeHandler.WaitForCompletionAsync();
    }

    private static Notification GetNotificationInfo(Offer offer, bool popupEnabled)
    {
        var message = Ensure.NotNull(offer.NotificationMessage, nameof(offer), nameof(offer.NotificationMessage));

        var notificationInfo = new Notification()
            .SetGroup(NotificationGroupId)
            .SetId(NotificationId)
            .SetHeaderText(message.HeaderText)
            .SetText(message.ContentText)
            .SetExpirationTime(offer.EndTimeUtc);

        if (!popupEnabled)
        {
            notificationInfo.SuppressPopup();
        }

        if (!string.IsNullOrEmpty(message.ButtonText))
        {
            notificationInfo = notificationInfo
                .AddButton(message.ButtonText, GetDealActionName);
        }

        if (!string.IsNullOrEmpty(message.LogoImageFilePath))
        {
            notificationInfo = notificationInfo
                .SetLogoImage(message.LogoImageFilePath);
        }

        return notificationInfo;
    }

    private void ScheduleExternalStateChangeHandling(bool forceRestart)
    {
        if (_isStopping)
        {
            return;
        }

        if (forceRestart)
        {
            _stateChangeHandler.Cancel();
        }

        _stateChangeHandler.Run();
    }

    private async Task HandleExternalStateChangeAsync(CancellationToken cancellationToken)
    {
        if (_onboardingState.Status is OnboardingStatus.Onboarding)
        {
            // Prevent showing offer notification during onboarding
            return;
        }

        var offer = _offer;

        if (offer?.NotificationMessage is not null)
        {
            await ShowNotificationIfNotYetShownAsync(offer, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            RemoveNotification();
        }
    }

    private async Task ShowNotificationIfNotYetShownAsync(Offer offer, CancellationToken cancellationToken)
    {
        if (!HasNotificationBeenShown(offer.Id))
        {
            await Task.Delay(_delayBeforeShowingNotification, cancellationToken).ConfigureAwait(false);

            ShowNotification(offer);
        }
    }

    private void ShowNotification(Offer offer)
    {
        _notificationService.ShowNotification(GetNotificationInfo(offer, _notificationPopupEnabled));

        MarkNotificationAsShown(offer.Id);
    }

    private void RemoveNotification()
    {
        _notificationService.RemoveNotificationGroup(NotificationGroupId);
    }

    private void OnNotificationServiceNotificationActivated(object? sender, NotificationActivatedEventArgs e)
    {
        if (e.GroupId != NotificationGroupId)
        {
            return;
        }

        // Note. If the app was not running when the user clicked on the system toast notification,
        // the app is started, but active notification is not yet available. Therefore, the
        // offer dialog is not shown.
        Schedule(OpenOfferAsync);
    }

    private async Task OpenOfferAsync()
    {
        var offer = _offer;
        if (offer is null)
        {
            return;
        }

        var dialog = _offerViewModelFactory.Invoke();
        if (!dialog.SetDataItem(offer))
        {
            return;
        }

        await _app.ActivateAsync().ConfigureAwait(true);

        _dialogService.ShowDialog(dialog);
    }

    private bool HasNotificationBeenShown(string id)
    {
        return _settingsRepository.Get()?.ShownNotifications.Any(n => n.Id == id) == true;
    }

    private void MarkNotificationAsShown(string id)
    {
        var shownNotifications = (_settingsRepository.Get() ?? new NotificationSettings()).ShownNotifications;

        if (shownNotifications.Any(n => n.Id == id))
        {
            return;
        }

        var now = _clock.UtcNow;
        var roundedTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, DateTimeKind.Utc);

        var notification = new ShownNotification { Id = id, ShowingTimeUtc = roundedTime };

        var settings = new NotificationSettings
        {
            ShownNotifications = shownNotifications.Prepend(notification).ToList().AsReadOnly(),
        };

        _settingsRepository.Set(settings);
    }

    private void Schedule(Func<Task> action)
    {
        _scheduler.Schedule(action);
    }
}
