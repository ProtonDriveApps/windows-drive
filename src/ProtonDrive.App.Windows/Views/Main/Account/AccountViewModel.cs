using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.Account;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Windows.Configuration.Hyperlinks;
using ProtonDrive.App.Windows.Toolkit.Threading;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.App.Windows.Views.Main.Account;

internal sealed class AccountViewModel : PageViewModel, IUserStateAware, ISessionStateAware
{
    private readonly IExternalHyperlinks _externalHyperlinks;
    private readonly IStatefulSessionService _sessionService;
    private readonly DispatcherScheduler _scheduler;
    private readonly AsyncRelayCommand _signOutCommand;

    private string? _planDisplayName = string.Empty;
    private UserType _userType;
    private long? _usedSpace;
    private long? _maxSpace;
    private UserQuotaStatus _userQuotaStatus;
    private SessionState _sessionState = SessionState.None;
    private string _username = string.Empty;
    private string _userEmailAddress = string.Empty;
    private string _userInitials = string.Empty;

    public AccountViewModel(IExternalHyperlinks externalHyperlinks, IStatefulSessionService sessionService, DispatcherScheduler scheduler)
    {
        _externalHyperlinks = externalHyperlinks;
        _sessionService = sessionService;
        _scheduler = scheduler;

        ManageAccountCommand = new RelayCommand(ManageAccount);
        UpgradePlanCommand = new RelayCommand(ManagePlan);
        ChangePasswordCommand = new RelayCommand(ChangePassword);
        ManageSessionsCommand = new RelayCommand(ManageSessions);
        ManagePlanCommand = new RelayCommand(ManagePlan);
        _signOutCommand = new AsyncRelayCommand(SignOutAsync, CanSignOut);
    }

    public ICommand ManageAccountCommand { get; }
    public ICommand UpgradePlanCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand ManageSessionsCommand { get; }
    public ICommand ManagePlanCommand { get; }
    public ICommand SignOutCommand => _signOutCommand;

    public string Username
    {
        get => _username;
        private set => SetProperty(ref _username, value);
    }

    public string UserEmailAddress
    {
        get => _userEmailAddress;
        private set => SetProperty(ref _userEmailAddress, value);
    }

    public string UserInitials
    {
        get => _userInitials;
        private set => SetProperty(ref _userInitials, value);
    }

    public UserType UserType
    {
        get => _userType;
        private set => SetProperty(ref _userType, value);
    }

    public string? PlanDisplayName
    {
        get => _planDisplayName;
        private set => SetProperty(ref _planDisplayName, value);
    }

    public long? UsedSpace
    {
        get => _usedSpace;
        private set => SetProperty(ref _usedSpace, value);
    }

    public long? MaxSpace
    {
        get => _maxSpace;
        private set => SetProperty(ref _maxSpace, value);
    }

    public UserQuotaStatus UserQuotaStatus
    {
        get => _userQuotaStatus;
        private set => SetProperty(ref _userQuotaStatus, value);
    }

    public void OnUserStateChanged(UserState userState)
    {
        Username = userState.DisplayName;
        UserInitials = userState.Initials;
        UserEmailAddress = userState.EmailAddress;
        UserType = userState.Type;
        UsedSpace = userState.UsedSpace;
        MaxSpace = userState.MaxSpace;
        UserQuotaStatus = userState.UserQuotaStatus;
        PlanDisplayName = userState.SubscriptionPlanDisplayName ?? userState.OrganizationDisplayName;
    }

    public void OnSessionStateChanged(SessionState value)
    {
        _sessionState = value;
        _scheduler.Schedule(() => _signOutCommand.NotifyCanExecuteChanged());
    }

    private bool CanSignOut()
    {
        return _sessionState.Status is not SessionStatus.NotStarted;
    }

    private Task SignOutAsync()
    {
        return !CanSignOut() ? Task.CompletedTask : _sessionService.EndSessionAsync();
    }

    private void ManagePlan()
    {
        _externalHyperlinks.Dashboard.Open();
    }

    private void ChangePassword()
    {
        _externalHyperlinks.ChangePassword.Open();
    }

    private void ManageSessions()
    {
        _externalHyperlinks.ManageSessions.Open();
    }

    private void ManageAccount()
    {
        _externalHyperlinks.ManageAccount.Open();
    }
}
