using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Volumes;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Sdk.Sync.Shared.SyncActivity;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Logging;
using Proton.Drive.Shared.Offline;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent;

internal sealed class SyncAgentStateProvider : ISessionStateAware, IAccountStateAware, IMainVolumeStateAware, IMappingsSetupStateAware, ISyncStateAware, IOfflineStateAware
{
    private readonly Lazy<IEnumerable<ISyncAgentStateAware>> _stateObservers;
    private readonly CoalescingAction _updateState;

    private SessionState _sessionState = SessionState.None;
    private AccountState _accountState = AccountState.None;
    private VolumeState _mainVolumeState = VolumeState.Idle;
    private MappingsSetupState _mappingsSetupState = MappingsSetupState.None;
    private SyncState _syncState = SyncState.Terminated;
    private bool _isOffline;
    private SyncAgentStatus _status = SyncAgentStatus.SignedOut;

    public SyncAgentStateProvider(Lazy<IEnumerable<ISyncAgentStateAware>> stateObservers, ILogger<SyncAgentStateProvider> logger)
    {
        _stateObservers = stateObservers;

        _updateState = logger.GetCoalescingActionWithExceptionsLogging(UpdateState, nameof(SyncAgentStateProvider));
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        _sessionState = value;
        _updateState.Run();
    }

    void IOfflineStateAware.OnOfflineStateChanged(OfflineStatus status)
    {
        _isOffline = status != OfflineStatus.Online;
        _updateState.Run();
    }

    void IAccountStateAware.OnAccountStateChanged(AccountState value)
    {
        _accountState = value;
        _updateState.Run();
    }

    void IMainVolumeStateAware.OnMainVolumeStateChanged(VolumeState value)
    {
        _mainVolumeState = value;
        _updateState.Run();
    }

    void IMappingsSetupStateAware.OnMappingsSetupStateChanged(MappingsSetupState value)
    {
        _mappingsSetupState = value;
        _updateState.Run();
    }

    void ISyncStateAware.OnSyncStateChanged(SyncState value)
    {
        _syncState = value;
        _updateState.Run();
    }

    internal Task WaitForCompletionAsync()
    {
        return _updateState.WaitForCompletionAsync();
    }

    private void UpdateState()
    {
        var status = GetStatus();

        if (ValueExtensions.TryUpdate(ref _status, status))
        {
            OnStateChanged(status);
        }
    }

    private void OnStateChanged(SyncAgentStatus status)
    {
        foreach (var listener in _stateObservers.Value)
        {
            listener.OnSyncAgentStateChanged(status);
        }
    }

    private SyncAgentStatus GetStatus()
    {
        return _sessionState.Status switch
        {
            SessionStatus.NotStarted => SyncAgentStatus.SignedOut,
            SessionStatus.Starting => SyncAgentStatus.SigningIn,
            SessionStatus.SigningIn => SyncAgentStatus.SigningIn,
            SessionStatus.Started => _isOffline switch
            {
                true => SyncAgentStatus.Offline,
                false => _accountState.Status switch
                {
                    AccountStatus.None => SyncAgentStatus.SettingUp,
                    AccountStatus.SettingUp => SyncAgentStatus.SettingUp,
                    AccountStatus.Succeeded => _mainVolumeState.Status switch
                    {
                        VolumeStatus.Idle => SyncAgentStatus.SettingUp,
                        VolumeStatus.SettingUp => SyncAgentStatus.SettingUp,
                        VolumeStatus.Ready => _mappingsSetupState.Status switch
                        {
                            MappingSetupStatus.None => SyncAgentStatus.SettingUp,
                            MappingSetupStatus.SettingUp => SyncAgentStatus.SettingUp,
                            MappingSetupStatus.Succeeded or MappingSetupStatus.PartiallySucceeded => _syncState.Status switch
                            {
                                SyncStatus.Terminated => SyncAgentStatus.SettingUp,
                                SyncStatus.Initializing => SyncAgentStatus.SettingUp,
                                SyncStatus.Paused => SyncAgentStatus.SynchronizationPaused,
                                SyncStatus.Idle => _mappingsSetupState.Status switch
                                {
                                    MappingSetupStatus.PartiallySucceeded => SyncAgentStatus.SynchronizationWarning,
                                    _ => _syncState.Failed
                                        ? SyncAgentStatus.SynchronizationWarning
                                        : SyncAgentStatus.Synchronized,
                                },
                                SyncStatus.DetectingUpdates => SyncAgentStatus.Synchronizing,
                                SyncStatus.Synchronizing => SyncAgentStatus.Synchronizing,
                                SyncStatus.Terminating => SyncAgentStatus.FinishingUp,
                                SyncStatus.Offline => SyncAgentStatus.Offline,
                                SyncStatus.Failed => SyncAgentStatus.SynchronizationError,
                                _ => throw new ArgumentOutOfRangeException(nameof(_syncState), _syncState.Status, null),
                            },
                            MappingSetupStatus.Failed => SyncAgentStatus.SyncFolderError,
                            _ => throw new ArgumentOutOfRangeException(nameof(_mappingsSetupState), _mappingsSetupState.Status, null),
                        },
                        VolumeStatus.Failed => SyncAgentStatus.AccountError,
                        _ => throw new ArgumentOutOfRangeException(nameof(_mainVolumeState), _mainVolumeState.Status, null),
                    },
                    AccountStatus.Failed => SyncAgentStatus.AccountError,
                    _ => throw new ArgumentOutOfRangeException(nameof(_accountState), _accountState.Status, null),
                },
            },
            SessionStatus.Ending => SyncAgentStatus.SigningOut,
            SessionStatus.Failed => _isOffline switch
            {
                true => SyncAgentStatus.Offline,
                false => SyncAgentStatus.SignInError,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(_sessionState), _sessionState.Status, null),
        };
    }
}
