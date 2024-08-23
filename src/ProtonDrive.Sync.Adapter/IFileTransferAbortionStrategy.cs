using System.Threading;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.Sync.Adapter;

public interface IFileTransferAbortionStrategy<TAltId>
{
    CancellationToken HandleFileOpenedForReading(LooseCompoundAltIdentity<TAltId> altId);
    void HandleFileClosed(LooseCompoundAltIdentity<TAltId> altId);
    void HandleFileChanged(LooseCompoundAltIdentity<TAltId> altId);
}
