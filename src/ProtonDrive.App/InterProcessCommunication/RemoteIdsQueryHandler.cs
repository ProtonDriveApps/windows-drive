using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Sync;

namespace ProtonDrive.App.InterProcessCommunication;

internal sealed class RemoteIdsQueryHandler : IpcMessageHandlerBase<string>
{
    private readonly IRemoteIdsFromLocalPathProvider _remoteIdsFromLocalPathProvider;

    public RemoteIdsQueryHandler(IRemoteIdsFromLocalPathProvider remoteIdsFromLocalPathProvider)
        : base(IpcMessageType.RemoteIdsQuery)
    {
        _remoteIdsFromLocalPathProvider = remoteIdsFromLocalPathProvider;
    }

    public override async Task HandleAsync<T>(string? path, T responder, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var remoteIds = await _remoteIdsFromLocalPathProvider.GetRemoteIdsOrDefaultAsync(path, cancellationToken).ConfigureAwait(false);
        if (remoteIds is null)
        {
            await responder.Respond(default(object), cancellationToken).ConfigureAwait(false);
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        await responder.Respond(new Response(remoteIds.Value.VolumeId, remoteIds.Value.ShareId, remoteIds.Value.LinkId), cancellationToken).ConfigureAwait(false);
    }

    private record Response(string VolumeId, string ShareId, string LinkId);
}
