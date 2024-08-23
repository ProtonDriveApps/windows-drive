using System.Collections.Generic;

namespace ProtonDrive.Update.Updates;

internal interface IBaseAppUpdateState
{
    IReadOnlyList<IRelease> ReleaseHistory();
    bool IsAvailable { get; }
    bool IsReady { get; }
    string FilePath { get; }
    string? FileArguments { get; }
}
