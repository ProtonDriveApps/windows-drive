using System.Collections.Generic;

namespace ProtonDrive.Update.Updates;

/// <inheritdoc />
internal class AppUpdateState : IAppUpdateState
{
    internal AppUpdateState(IBaseAppUpdateState update, AppUpdateStatus status)
    {
        ReleaseHistory = update.ReleaseHistory();
        IsAvailable = update.IsAvailable;
        IsReady = update.IsReady;
        Status = status;
    }

    public IReadOnlyList<IRelease> ReleaseHistory { get; }
    public bool IsAvailable { get; }
    public bool IsReady { get; }
    public AppUpdateStatus Status { get; }
}
