using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.App.Sanitization;

internal sealed record SynchronizationIdentities(
    string RemoteAltId,
    long LocalParentAltId,
    long LocalAltId,
    string Path,
    RootInfo<long> LocalRootInfo,
    RootInfo<string> RemoteRootInfo)
{
    public LooseCompoundAltIdentity<string> InternalRemoteCompoundId => (RemoteRootInfo.VolumeId, RemoteAltId);
    public LooseCompoundAltIdentity<long> InternalLocalCompoundId => (LocalRootInfo.VolumeId, LocalAltId);
}
