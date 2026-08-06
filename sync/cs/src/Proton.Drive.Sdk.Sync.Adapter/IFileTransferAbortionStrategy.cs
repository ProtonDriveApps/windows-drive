using Proton.Drive.Sdk.Sync.Shared.Trees;

namespace Proton.Drive.Sdk.Sync.Adapter;

public interface IFileTransferAbortionStrategy<TAltId>
{
    CancellationToken HandleFileOpenedForReading(LooseCompoundAltIdentity<TAltId> altId);
    void HandleFileClosed(LooseCompoundAltIdentity<TAltId> altId);
    void HandleFileChanged(LooseCompoundAltIdentity<TAltId> altId);
}
