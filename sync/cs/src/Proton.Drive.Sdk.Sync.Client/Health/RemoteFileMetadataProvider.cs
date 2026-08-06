using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Client.RemoteNodes;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.Client;
using Proton.Drive.Shared.Extensions;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Client.Health;

internal sealed class RemoteFileMetadataProvider(
    ILinkApiClient linkApiClient,
    IRemoteNodeService remoteNodeService,
    IErrorCounter errorCounter,
    ILogger<RemoteFileMetadataProvider> logger) : IRemoteFileMetadataProvider
{
    private readonly ILinkApiClient _linkApiClient = linkApiClient;
    private readonly IRemoteNodeService _remoteNodeService = remoteNodeService;
    private readonly IErrorCounter _errorCounter = errorCounter;
    private readonly ILogger<RemoteFileMetadataProvider> _logger = logger;

    public async Task<IReadOnlyList<NodeInfo<string>>> GetFileMetadataAsync(
        IReadOnlyList<string> remoteIds,
        string shareId,
        CancellationToken cancellationToken)
    {
        var startTimestamp = Stopwatch.GetTimestamp();

        var result = await InternalGetFileMetadataAsync(remoteIds, shareId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Fetched metadata of {Count} remote files in {Duration}", result.Count, Stopwatch.GetElapsedTime(startTimestamp));

        return result;
    }

    public async Task<IReadOnlyList<NodeInfo<string>>> InternalGetFileMetadataAsync(
        IReadOnlyList<string> remoteIds,
        string shareId,
        CancellationToken cancellationToken)
    {
        var result = new List<NodeInfo<string>>();

        try
        {
            var parameters = new FetchLinksMetadataParameters { LinkIds = remoteIds };
            var response = await _linkApiClient.GetLinksAsync(shareId, parameters, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

            foreach (var link in response.Links)
            {
                try
                {
                    var remoteNode = await GetRemoteNodeAsync(shareId, link, response.Parents, cancellationToken).ConfigureAwait(false);

                    result.Add(remoteNode.ToNodeInfo());
                }
                catch (Exception ex) when (ex.IsDriveClientException())
                {
                    _logger.LogWarning(
                        "Fetching remote file with ID \"{LinkId}\" on share with ID \"{ShareId}\" metadata failed: {ErrorMessage}",
                        link.Id,
                        shareId,
                        ex.CombinedMessage());
                    _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, ex);

                    if (ex is ApiException { ResponseCode: ResponseCode.Offline } apiException)
                    {
                        throw new FileSystemClientException(apiException.Message, FileSystemErrorCode.Offline);
                    }
                }
            }
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogWarning("Fetching {Count} remote files metadata failed: {ErrorMessage}", remoteIds.Count, ex.CombinedMessage());
            _errorCounter.Add(ErrorScope.DataIntegrityItemOperation, ex);

            if (ex is ApiException { ResponseCode: ResponseCode.Offline } apiException)
            {
                throw new FileSystemClientException(apiException.Message, FileSystemErrorCode.Offline);
            }
        }

        return result;
    }

    private async Task<RemoteNode> GetRemoteNodeAsync(string shareId, Link link, IReadOnlyList<Link> parents, CancellationToken cancellationToken)
    {
        var hierarchyFromRootToNode = MoreEnumerable.TraverseDepthFirst(link, l => parents.Where(parent => parent.Id == l.ParentId).Take(1)).Reverse();

        return await _remoteNodeService.GetRemoteNodeFromHierarchyAsync(shareId, hierarchyFromRootToNode, cancellationToken).ConfigureAwait(false);
    }
}
