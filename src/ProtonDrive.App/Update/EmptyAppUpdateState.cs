using ProtonDrive.Update;

namespace ProtonDrive.App.Update;

internal sealed class EmptyAppUpdateState : IAppUpdateState
{
    public IReadOnlyList<IRelease> ReleaseHistory => [];

    public bool IsAvailable => false;

    public bool IsReady => false;

    public AppUpdateStatus Status => AppUpdateStatus.UpToDate;
}
