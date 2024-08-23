using System;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.Sync.Adapter.Shared;

public static class CompoundIdentityExtensions
{
    public static LooseCompoundAltIdentity<TId> GetCompoundId<TId>(this NodeInfo<TId> nodeInfo)
    where TId : IEquatable<TId>
    {
        return (
            nodeInfo.Root?.VolumeId ?? throw new ArgumentException("Root is null"),
            nodeInfo.Id);
    }

    public static LooseCompoundAltIdentity<TId> GetCompoundParentId<TId>(this NodeInfo<TId> nodeInfo)
    where TId : IEquatable<TId>
    {
        return (
            nodeInfo.Root?.VolumeId ?? throw new ArgumentException("Root is null"),
            nodeInfo.ParentId);
    }

    public static LooseCompoundAltIdentity<TId> GetCompoundId<TId>(this EventLogEntry<TId> entry, int volumeId)
    where TId : IEquatable<TId>
    {
        return (volumeId, entry.Id);
    }

    public static LooseCompoundAltIdentity<TId> GetCompoundParentId<TId>(this EventLogEntry<TId> entry, int volumeId)
    where TId : IEquatable<TId>
    {
        return (volumeId, entry.ParentId);
    }
}
