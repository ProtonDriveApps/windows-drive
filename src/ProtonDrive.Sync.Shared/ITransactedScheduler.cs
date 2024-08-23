using ProtonDrive.Shared.Threading;

namespace ProtonDrive.Sync.Shared;

public interface ITransactedScheduler : IScheduler
{
    bool ForceCommit { set; }
}
