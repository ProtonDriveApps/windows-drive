using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Shared;

public interface ITransactedScheduler : IScheduler
{
    bool ForceCommit { set; }
}
