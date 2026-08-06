using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Proton.Drive.App.Windows.Configuration.Hyperlinks;
using Proton.Drive.App.Windows.Toolkit.Threading;
using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared.Authentication;

namespace Proton.Drive.App.Windows.Views.Main.Account;

internal sealed class AccountViewModel : PageViewModel, IUserStateAware, ISessionStateAware
{
    private readonly IExternalHyperlinks _externalHyperlinks;
    private readonly IStatefulSessionService _sessionService;
    private readonly DispatcherScheduler _scheduler;
    private readonly AsyncRelayCommand _signOutCommand;

    private SessionState _sessionState = SessionState.None;

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

    public string? Username
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string? UserEmailAddress
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string? UserInitials
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public UserType UserType
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public string? PlanDisplayName
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public long? UsedSpace
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public long? MaxSpace
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public UserQuotaStatus UserQuotaStatus
    {
        get;
        private set => SetProperty(ref field, value);
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
