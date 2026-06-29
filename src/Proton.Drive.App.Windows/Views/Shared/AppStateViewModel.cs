using CommunityToolkit.Mvvm.ComponentModel;
using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Volumes;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Offline;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Windows.Views.Shared;

internal sealed class AppStateViewModel : ObservableObject, ISessionStateAware, IAccountStateAware, IMainVolumeStateAware, IMappingsSetupStateAware, ISyncStateAware, IOfflineStateAware, IUserStateAware, IDisposable
{
    private static readonly TimeSpan TimerInterval = TimeSpan.FromSeconds(20);

    private readonly AppConfig _appConfig;
    private readonly CoalescingAction _updateAppState;
    private readonly ISchedulerTimer _timer;

    private AppIconStatus _iconStatus;
    private AppDisplayStatus _displayStatus;
    private DateTime _lastSynchronizedAt;
    private UserState? _user;
    private SessionState _sessionState = SessionState.None;
    private AccountState _accountState = AccountState.None;
    private VolumeState _mainVolumeState = VolumeState.Idle;
    private MappingsSetupState _mappingsSetupState = MappingsSetupState.None;
    private SyncState _syncState = SyncState.Terminated;
    private bool _isOffline;

    public AppStateViewModel(AppConfig appConfig, IScheduler scheduler)
    {
        _appConfig = appConfig;
        _updateAppState = new CoalescingAction(UpdateAppState);

        _timer = scheduler.CreateTimer();
        _timer.Interval = TimerInterval;
        _timer.Tick += TimerOnTick;
    }

    public AppIconStatus IconStatus
    {
        get => _iconStatus;
        private set => SetProperty(ref _iconStatus, value);
    }

    public AppDisplayStatus DisplayStatus
    {
        get => _displayStatus;
        private set => SetProperty(ref _displayStatus, value);
    }

    public DateTime LastSynchronizedAt
    {
        get => _lastSynchronizedAt;
        private set => SetProperty(ref _lastSynchronizedAt, value);
    }

    public UserState? User
    {
        get => _user;
        private set => SetProperty(ref _user, value);
    }

    public Version AppVersion => _appConfig.AppVersion;

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        _sessionState = value;
        _updateAppState.Run();
    }

    void IOfflineStateAware.OnOfflineStateChanged(OfflineStatus status)
    {
        _isOffline = status != OfflineStatus.Online;
        _updateAppState.Run();
    }

    void IAccountStateAware.OnAccountStateChanged(AccountState value)
    {
        _accountState = value;
        _updateAppState.Run();
    }

    void IMainVolumeStateAware.OnMainVolumeStateChanged(VolumeState value)
    {
        _mainVolumeState = value;
        _updateAppState.Run();
    }

    void IMappingsSetupStateAware.OnMappingsSetupStateChanged(MappingsSetupState value)
    {
        _mappingsSetupState = value;
        _updateAppState.Run();
    }

    void ISyncStateAware.OnSyncStateChanged(SyncState value)
    {
        _syncState = value;

        // Idle is the only SyncStatus value when the time past last synchronization is displayed.
        if (value.Status is SyncStatus.Idle)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }

        _updateAppState.Run();
    }

    void IUserStateAware.OnUserStateChanged(UserState userState)
    {
        User = userState.IsEmpty ? null : userState;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    private void UpdateAppState()
    {
        if (DisplayStatus == AppDisplayStatus.Synchronizing)
        {
            LastSynchronizedAt = DateTime.UtcNow;
        }

        (IconStatus, DisplayStatus) = GetStatus();
    }

    private (AppIconStatus IconStatus, AppDisplayStatus DisplayStatus) GetStatus()
    {
        return _sessionState.Status switch
        {
            SessionStatus.NotStarted => (AppIconStatus.Inactive, AppDisplayStatus.SignedOut),
            SessionStatus.Starting => (AppIconStatus.Active, AppDisplayStatus.SigningIn),
            SessionStatus.SigningIn => (AppIconStatus.Active, AppDisplayStatus.SigningIn),
            SessionStatus.Started => _isOffline switch
            {
                true => (AppIconStatus.Offline, AppDisplayStatus.Offline),
                false => _accountState.Status switch
                {
                    AccountStatus.None => (AppIconStatus.Active, AppDisplayStatus.SettingUp),
                    AccountStatus.SettingUp => (AppIconStatus.Active, AppDisplayStatus.SettingUp),
                    AccountStatus.Succeeded => _mainVolumeState.Status switch
                    {
                        VolumeStatus.Idle => (AppIconStatus.Active, AppDisplayStatus.SettingUp),
                        VolumeStatus.SettingUp => (AppIconStatus.Active, AppDisplayStatus.SettingUp),
                        VolumeStatus.Ready => _mappingsSetupState.Status switch
                        {
                            MappingSetupStatus.None => (AppIconStatus.Active, AppDisplayStatus.SettingUp),
                            MappingSetupStatus.SettingUp => (AppIconStatus.Active, AppDisplayStatus.SettingUp),
                            MappingSetupStatus.SettingUp or MappingSetupStatus.Succeeded or MappingSetupStatus.PartiallySucceeded => _syncState.Status switch
                            {
                                SyncStatus.Terminated => (AppIconStatus.Active, AppDisplayStatus.SettingUp),
                                SyncStatus.Initializing => (AppIconStatus.Active, AppDisplayStatus.SettingUp),
                                SyncStatus.Paused => (AppIconStatus.Paused, AppDisplayStatus.SynchronizationPaused),
                                SyncStatus.Idle => _mappingsSetupState.Status switch
                                {
                                    MappingSetupStatus.PartiallySucceeded => (AppIconStatus.Warning, AppDisplayStatus.SynchronizationWarning),
                                    _ => _syncState.Failed
                                        ? (AppIconStatus.Warning, AppDisplayStatus.SynchronizationWarning)
                                        : (AppIconStatus.Synchronized, AppDisplayStatus.Synchronized),
                                },
                                SyncStatus.DetectingUpdates => (AppIconStatus.Synchronizing, AppDisplayStatus.Synchronizing),
                                SyncStatus.Synchronizing => (AppIconStatus.Synchronizing, AppDisplayStatus.Synchronizing),
                                SyncStatus.Terminating => (AppIconStatus.Active, AppDisplayStatus.FinishingUp),
                                SyncStatus.Offline => (AppIconStatus.Offline, AppDisplayStatus.Offline),
                                SyncStatus.Failed => (AppIconStatus.Error, AppDisplayStatus.SynchronizationError),
                                _ => throw new ArgumentOutOfRangeException(),
                            },
                            MappingSetupStatus.Failed => (AppIconStatus.Error, AppDisplayStatus.SyncFolderError),
                            _ => throw new ArgumentOutOfRangeException(),
                        },
                        VolumeStatus.Failed => (AppIconStatus.Error, AppDisplayStatus.AccountError),
                        _ => throw new ArgumentOutOfRangeException(),
                    },
                    AccountStatus.Failed => (AppIconStatus.Error, AppDisplayStatus.AccountError),
                    _ => throw new ArgumentOutOfRangeException(),
                },
            },
            SessionStatus.Ending => (AppIconStatus.Inactive, AppDisplayStatus.SigningOut),
            SessionStatus.Failed => _isOffline switch
            {
                true => (AppIconStatus.Offline, AppDisplayStatus.Offline),
                false => (AppIconStatus.Error, AppDisplayStatus.SignInError),
            },
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        // Pretend value has changed for the value converter to update the time passed
        OnPropertyChanged(nameof(LastSynchronizedAt));
    }
}
