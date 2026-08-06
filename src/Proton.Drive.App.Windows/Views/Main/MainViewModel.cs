using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Proton.Drive.App.Notifications.Offers;
using Proton.Drive.App.Photos;
using Proton.Drive.App.Windows.Configuration.Hyperlinks;
using Proton.Drive.App.Windows.Services;
using Proton.Drive.App.Windows.Views.BugReport;
using Proton.Drive.App.Windows.Views.Main.Account;
using Proton.Drive.App.Windows.Views.Offer;
using Proton.Drive.App.Windows.Views.Shared;
using Proton.Drive.App.Windows.Views.Shared.Navigation;
using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Windows.Views.Main;

internal sealed class MainViewModel
    : ObservableObject, IApplicationPages, IUserStateAware, ISessionStateAware, IAccountStateAware, IOffersAware, IPhotosFeatureStateAware
{
    private readonly IApp _app;
    private readonly PageViewModelFactory _pageViewModelFactory;
    private readonly Func<BugReportViewModel> _bugReportViewModelFactory;
    private readonly Func<OfferViewModel> _offerViewModelFactory;
    private readonly IDialogService _dialogService;
    private readonly IExternalHyperlinks _externalHyperlinks;
    private readonly IUpgradeStoragePlanAvailabilityVerifier _upgradeStoragePlanAvailabilityVerifier;
    private readonly IScheduler _scheduler;

    private ApplicationPage _currentMenuItem;
    private IconStatus _accountIconStatus;
    private AccountDisplayStatus _accountDisplayStatus;
    private AccountErrorCode? _errorCode;
    private PageViewModel _page;

    private UserState? _user;
    private SessionState _sessionState = SessionState.None;
    private AccountState _accountState = AccountState.None;

    private Notifications.Offers.Offer? _offer;
    private string? _offerTitle;
    private bool _photosFeatureIsVisible;

    public MainViewModel(
        IApp app,
        INavigationService<DetailsPageViewModel> detailsPages,
        AppStateViewModel stateViewModel,
        PageViewModelFactory pageViewModelFactory,
        Func<BugReportViewModel> bugReportViewModelFactory,
        Func<OfferViewModel> offerViewModelFactory,
        IDialogService dialogService,
        IExternalHyperlinks externalHyperlinks,
        IUpgradeStoragePlanAvailabilityVerifier upgradeStoragePlanAvailabilityVerifier,
        NotificationBadgeProvider notificationBadges,
        [FromKeyedServices("Dispatcher")] IScheduler scheduler)
    {
        _app = app;
        _pageViewModelFactory = pageViewModelFactory;
        _bugReportViewModelFactory = bugReportViewModelFactory;
        _offerViewModelFactory = offerViewModelFactory;
        _dialogService = dialogService;
        _externalHyperlinks = externalHyperlinks;
        _upgradeStoragePlanAvailabilityVerifier = upgradeStoragePlanAvailabilityVerifier;
        _scheduler = scheduler;

        AppState = stateViewModel;
        NotificationBadges = notificationBadges;
        DetailsPages = detailsPages;
        OpenAccountPageCommand = new RelayCommand(() => CurrentMenuItem = ApplicationPage.Account);

        ReportBugCommand = new RelayCommand(ReportBug);
        GetMoreStorageCommand = new RelayCommand(GetMoreStorage, CanGetMoreStorage);
        OpenOfferCommand = new RelayCommand(OpenOffer, CanOpenOffer);
        OpenWebDashboardCommand = new RelayCommand(OpenWebDashboard);

        _page = ToPageViewModel(CurrentMenuItem);
    }

    public INavigationService<DetailsPageViewModel> DetailsPages { get; }

    public ApplicationPage CurrentMenuItem
    {
        get => _currentMenuItem;
        set
        {
            if (SetProperty(ref _currentMenuItem, value))
            {
                Page = ToPageViewModel(value);
            }
        }
    }

    public ICommand OpenAccountPageCommand { get; }
    public ICommand OpenWebDashboardCommand { get; }
    public IRelayCommand GetMoreStorageCommand { get; }
    public IRelayCommand OpenOfferCommand { get; }
    public ICommand ReportBugCommand { get; }

    public PageViewModel Page
    {
        get => _page;
        private set
        {
            if (SetProperty(ref _page, value))
            {
                value.OnActivated();
            }
        }
    }

    public UserState? User
    {
        get => _user;
        set
        {
            if (SetProperty(ref _user, value))
            {
                Schedule(RefreshCommandsAndStatuses);
            }
        }
    }

    public AppStateViewModel AppState { get; }

    public NotificationBadgeProvider NotificationBadges { get; }

    public IconStatus AccountIconStatus
    {
        get => _accountIconStatus;
        private set => SetProperty(ref _accountIconStatus, value);
    }

    public AccountDisplayStatus AccountDisplayStatus
    {
        get => _accountDisplayStatus;
        private set => SetProperty(ref _accountDisplayStatus, value);
    }

    public AccountErrorCode? ErrorCode
    {
        get => _errorCode;
        private set => SetProperty(ref _errorCode, value);
    }

    public bool PhotosFeatureIsVisible
    {
        get => _photosFeatureIsVisible;
        private set => SetProperty(ref _photosFeatureIsVisible, value);
    }

    public string? OfferTitle
    {
        get => _offerTitle;
        private set => SetProperty(ref _offerTitle, value);
    }

    void IApplicationPages.Show(ApplicationPage page)
    {
        Schedule(() =>
        {
            _app.ActivateAsync();
            ShowPage(page);
        });
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        _sessionState = value;
        Schedule(RefreshCommandsAndStatuses);
    }

    void IAccountStateAware.OnAccountStateChanged(AccountState value)
    {
        _accountState = value;
        Schedule(RefreshCommandsAndStatuses);
    }

    void IUserStateAware.OnUserStateChanged(UserState value)
    {
        User = value.IsEmpty ? null : value;
    }

    void IPhotosFeatureStateAware.OnPhotosFeatureStateChanged(PhotosFeatureState value)
    {
        Schedule(() => RefreshPhotosFeatureVisibility(value.Status));
    }

    void IOffersAware.OnActiveOfferChanged(Notifications.Offers.Offer? offer)
    {
        _offer = offer;
        OfferTitle = offer?.Title;

        Schedule(() =>
        {
            GetMoreStorageCommand.NotifyCanExecuteChanged();
            OpenOfferCommand.NotifyCanExecuteChanged();
        });
    }

    private void ShowPage(ApplicationPage page)
    {
        CurrentMenuItem = page;
    }

    private PageViewModel ToPageViewModel(ApplicationPage page)
    {
        return _pageViewModelFactory.Create(page) ?? _page;
    }

    private void RefreshPhotosFeatureVisibility(PhotosFeatureStatus status)
    {
        PhotosFeatureIsVisible = status is not PhotosFeatureStatus.Hidden;
    }

    private void RefreshCommandsAndStatuses()
    {
        AccountIconStatus = GetAccountIconStatus();
        AccountDisplayStatus = GetAccountDisplayStatus();
        ErrorCode = _accountState.ErrorCode;

        GetMoreStorageCommand.NotifyCanExecuteChanged();
    }

    private AccountDisplayStatus GetAccountDisplayStatus()
    {
        return _sessionState.Status switch
        {
            SessionStatus.NotStarted => AccountDisplayStatus.SignedOut,
            SessionStatus.Starting => AccountDisplayStatus.SigningIn,
            SessionStatus.SigningIn => AccountDisplayStatus.SigningIn,
            SessionStatus.Started => _accountState.Status switch
            {
                AccountStatus.None => AccountDisplayStatus.SettingUp,
                AccountStatus.SettingUp => AccountDisplayStatus.SettingUp,
                AccountStatus.Succeeded => AccountDisplayStatus.Succeeded,
                AccountStatus.Failed => AccountDisplayStatus.AccountError,
                _ => throw new ArgumentOutOfRangeException(),
            },
            SessionStatus.Ending => AccountDisplayStatus.SigningOut,
            SessionStatus.Failed => AccountDisplayStatus.SessionError,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private IconStatus GetAccountIconStatus()
    {
        return _sessionState.Status switch
        {
            SessionStatus.NotStarted => IconStatus.None,
            SessionStatus.Starting => IconStatus.None,
            SessionStatus.SigningIn => IconStatus.None,
            SessionStatus.Started => _accountState.Status switch
            {
                AccountStatus.None => IconStatus.None,
                AccountStatus.SettingUp => IconStatus.None,
                AccountStatus.Succeeded => IconStatus.Success,
                AccountStatus.Failed => IconStatus.Error,
                _ => throw new ArgumentOutOfRangeException(),
            },
            SessionStatus.Ending => IconStatus.None,
            SessionStatus.Failed => IconStatus.Error,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private void ReportBug()
    {
        var dialog = _bugReportViewModelFactory.Invoke();
        _dialogService.Show(dialog);
    }

    private bool CanGetMoreStorage()
    {
        return !CanOpenOffer() && _upgradeStoragePlanAvailabilityVerifier.UpgradedPlanIsAvailable(UpgradeStoragePlanMode.Sidebar, _user?.SubscriptionPlanCode);
    }

    private void GetMoreStorage()
    {
        _externalHyperlinks.UpgradePlanFromSidebar.Open();
    }

    private bool CanOpenOffer()
    {
        return _offer is not null;
    }

    private void OpenOffer()
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

        _dialogService.ShowDialog(dialog);
    }

    private void OpenWebDashboard()
    {
        _externalHyperlinks.Dashboard.Open();
    }

    private void Schedule(Action action)
    {
        _scheduler.Schedule(action);
    }
}
