using System.Collections.Generic;
using ProtonDrive.Update;

namespace ProtonDrive.App.Update;

public sealed class UpdateState : IAppUpdateState
{
    private readonly IAppUpdateState _state;

    public UpdateState(IAppUpdateState state)
    {
        _state = state;
    }

    public bool UpdateRequired { get; init; }

    public bool ManualCheck { get; init; }

    public IReadOnlyList<IRelease> ReleaseHistory => _state.ReleaseHistory;

    public bool IsAvailable => _state.IsAvailable;

    public bool IsReady => _state.IsReady;

    public AppUpdateStatus Status => _state.Status;
}
