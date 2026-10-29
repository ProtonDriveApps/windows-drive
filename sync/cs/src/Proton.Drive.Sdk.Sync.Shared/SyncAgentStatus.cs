namespace Proton.Drive.Sdk.Sync.Shared;

public enum SyncAgentStatus
{
    SignedOut,
    SigningIn,
    SettingUp,
    AccountError,
    SyncFolderError,
    SynchronizationPaused,
    Synchronizing,
    Synchronized,
    SynchronizationWarning,
    SynchronizationError,
    FinishingUp,
    SigningOut,
    SignInError,
    Offline,
}
