using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Agent;

internal sealed class RemoteIdsFromLocalPathProvider : IRemoteIdsFromLocalPathProvider, IMappingsAware
{
    private readonly IFileSystemIdentityProvider<long> _fileSystemIdentityProvider;
    private readonly IMappedFileSystemIdentityProvider _mappedFileSystemIdentityProvider;

    private IReadOnlyCollection<RemoteToLocalMapping> _activeMappings = [];

    public RemoteIdsFromLocalPathProvider(
        IFileSystemIdentityProvider<long> fileSystemIdentityProvider,
        IMappedFileSystemIdentityProvider mappedFileSystemIdentityProvider)
    {
        _fileSystemIdentityProvider = fileSystemIdentityProvider;
        _mappedFileSystemIdentityProvider = mappedFileSystemIdentityProvider;
    }

    async Task<RemoteIds?> IRemoteIdsFromLocalPathProvider.GetRemoteIdsOrDefaultAsync(string localPath, CancellationToken cancellationToken)
    {
        if (!_fileSystemIdentityProvider.TryGetIdFromPath(localPath, out var localFileId))
        {
            return default;
        }

        var mapping = _activeMappings.FirstOrDefault(
            m =>
                m is { HasSetupSucceeded: true, Type: not MappingType.SharedWithMeRootFolder } &&
                (m.Remote.RootItemType is LinkType.File
                    ? string.Equals(m.Local.Path, localPath, StringComparison.OrdinalIgnoreCase)
                    : PathComparison.IsAncestor(m.Local.Path, localPath)));

        if (mapping is not { Remote: { VolumeId: not null, ShareId: not null } })
        {
            return default;
        }

        var localVolumeId = mapping.Local.InternalVolumeId;
        var remoteId = await _mappedFileSystemIdentityProvider
            .GetRemoteIdFromLocalIdOrDefaultAsync((localVolumeId, localFileId), cancellationToken)
            .ConfigureAwait(false);

        return remoteId?.ItemId is not null ? new RemoteIds(mapping.Remote.VolumeId, mapping.Remote.ShareId, LinkId: remoteId.Value.ItemId) : null;
    }

    void IMappingsAware.OnMappingsChanged(IReadOnlyCollection<RemoteToLocalMapping> activeMappings, IReadOnlyCollection<RemoteToLocalMapping> deletedMappings)
    {
        _activeMappings = activeMappings;
    }
}
