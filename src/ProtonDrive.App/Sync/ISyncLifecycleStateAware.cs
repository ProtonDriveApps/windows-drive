namespace ProtonDrive.App.Sync;

internal enum SyncLifecycleStatus
{
    Disabled,
    Enabled,
}

internal interface ISyncLifecycleStateAware
{
    void OnSyncLifecycleStateChanged(SyncLifecycleStatus value);
}
