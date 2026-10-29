using CommunityToolkit.Mvvm.ComponentModel;
using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Windows.Views.Shared;

internal sealed class AppStateViewModel : ObservableObject, ISyncAgentStateAware, IUserStateAware, IDisposable
{
    internal static readonly IReadOnlyDictionary<SyncAgentStatus, AppIconStatus> SyncAgentStatusToAppIconStatusMap = new Dictionary<SyncAgentStatus, AppIconStatus>()
    {
        { SyncAgentStatus.SignedOut, AppIconStatus.Inactive },
        { SyncAgentStatus.SigningIn, AppIconStatus.Active },
        { SyncAgentStatus.SettingUp, AppIconStatus.Active },
        { SyncAgentStatus.AccountError, AppIconStatus.Error },
        { SyncAgentStatus.SyncFolderError, AppIconStatus.Error },
        { SyncAgentStatus.SynchronizationPaused, AppIconStatus.Paused },
        { SyncAgentStatus.Synchronizing, AppIconStatus.Synchronizing },
        { SyncAgentStatus.Synchronized, AppIconStatus.Synchronized },
        { SyncAgentStatus.SynchronizationWarning, AppIconStatus.Warning },
        { SyncAgentStatus.SynchronizationError, AppIconStatus.Error },
        { SyncAgentStatus.FinishingUp, AppIconStatus.Active },
        { SyncAgentStatus.SigningOut, AppIconStatus.Inactive },
        { SyncAgentStatus.SignInError, AppIconStatus.Error },
        { SyncAgentStatus.Offline, AppIconStatus.Offline },
    }.AsReadOnly();

    private static readonly TimeSpan TimerInterval = TimeSpan.FromSeconds(20);

    private readonly AppConfig _appConfig;

    private readonly ISchedulerTimer _timer;

    public AppStateViewModel(AppConfig appConfig, IScheduler scheduler)
    {
        _appConfig = appConfig;

        _timer = scheduler.CreateTimer();
        _timer.Interval = TimerInterval;
        _timer.Tick += TimerOnTick;
    }

    public AppIconStatus IconStatus
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public SyncAgentStatus DisplayStatus
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public DateTime LastSynchronizationTime
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public UserState? User
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public Version AppVersion => _appConfig.AppVersion;

    void ISyncAgentStateAware.OnSyncAgentStateChanged(SyncAgentStatus status)
    {
        if (DisplayStatus == SyncAgentStatus.Synchronizing)
        {
            LastSynchronizationTime = DateTime.UtcNow;
        }

        DisplayStatus = status;
        IconStatus = SyncAgentStatusToAppIconStatusMap.GetValueOrDefault(status);

        // Synchronized is the only SyncAgentStatus value when the time past last synchronization is displayed
        if (status is SyncAgentStatus.Synchronized)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    void IUserStateAware.OnUserStateChanged(UserState userState)
    {
        User = userState.IsEmpty ? null : userState;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        // Pretend value has changed for the value converter to update the time passed
        OnPropertyChanged(nameof(LastSynchronizationTime));
    }
}
