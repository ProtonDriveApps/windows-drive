using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Settings;
using ProtonDrive.App.Sync;

namespace ProtonDrive.App.InterProcessCommunication;

internal sealed class SyncRootPathsQueryHandler : IpcMessageHandlerBase<IReadOnlyList<MappingType>>
{
    private readonly ISyncRootPathProvider _syncRootPathProvider;

    public SyncRootPathsQueryHandler(ISyncRootPathProvider syncRootPathProvider)
        : base(IpcMessageType.SyncRootPathsQuery)
    {
        _syncRootPathProvider = syncRootPathProvider;
    }

    public override async Task HandleAsync<T>(IReadOnlyList<MappingType>? mappingTypes, T responder, CancellationToken cancellationToken)
    {
        var syncRootPaths = _syncRootPathProvider.GetOfTypes(mappingTypes ?? Enum.GetValues<MappingType>());

        cancellationToken.ThrowIfCancellationRequested();

        await responder.Respond(syncRootPaths, cancellationToken).ConfigureAwait(false);
    }
}
