using Proton.Drive.Update;

namespace Proton.Drive.App.Update;

public sealed class UpdateState(IAppUpdateState state) : IAppUpdateState
{
    private readonly IAppUpdateState _state = state;

    public bool UpdateRequired { get; init; }

    public bool ManualCheck { get; init; }

    public IReadOnlyList<IRelease> ReleaseHistory => _state.ReleaseHistory;

    public bool IsAvailable => _state.IsAvailable;

    public bool IsReady => _state.IsReady;

    public AppUpdateStatus Status => _state.Status;
}
