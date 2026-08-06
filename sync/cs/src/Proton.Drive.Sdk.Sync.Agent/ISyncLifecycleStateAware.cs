namespace Proton.Drive.Sdk.Sync.Agent;

internal enum SyncLifecycleStatus
{
    Disabled,
    Enabled,
}

internal interface ISyncLifecycleStateAware
{
    void OnSyncLifecycleStateChanged(SyncLifecycleStatus value);
}
