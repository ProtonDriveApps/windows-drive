using System;
using System.Collections.Generic;
using ProtonDrive.Update;

namespace ProtonDrive.App.Update;

internal sealed class EmptyAppUpdateState : IAppUpdateState
{
    public IReadOnlyList<IRelease> ReleaseHistory => Array.Empty<IRelease>();

    public bool IsAvailable => false;

    public bool IsReady => false;

    public AppUpdateStatus Status => AppUpdateStatus.None;
}
