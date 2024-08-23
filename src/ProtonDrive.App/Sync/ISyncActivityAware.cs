using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Sync;

public interface ISyncActivityAware
{
    /// <summary>
    /// Occurs when synchronization activity has changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sequence of events affecting the same item can contain the same
    /// item instance, but not guaranteed. Item is uniquely identified by the combination
    /// of <see cref="SyncActivityItem{T}.Replica"/> and <see cref="SyncActivityItem{T}.Id"/>
    /// values.
    /// </para>
    /// <para>
    /// The <see cref="SyncService"/> can raise events concurrently. Concurrent events will never
    /// contain the same item.
    /// </para>
    /// </remarks>
    /// <param name="item">The item that is affected by the change.</param>
    void OnSyncActivityChanged(SyncActivityItem<long> item);
}
