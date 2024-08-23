using System;

namespace ProtonDrive.Sync.Shared.SyncActivity;

public sealed class SyncActivityChangedEventArgs<TId> : EventArgs
{
    public SyncActivityChangedEventArgs(SyncActivityItem<TId> item)
    {
        Item = item;
    }

    public SyncActivityItem<TId> Item { get; init; }
}
