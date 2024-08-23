using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.RemoteNodes;
using ProtonDrive.Shared.Telemetry;

namespace ProtonDrive.Client.Shares.SharedWithMe;

internal sealed class SharedWithMeClient : ISharedWithMeClient
{
    private const int MaxNumberOfParallelism = 5; // Max number of shared with me items that can be created at a time

    private readonly ILinkApiClient _linkApiClient;
    private readonly IShareApiClient _shareApiClient;
    private readonly IRemoteNodeService _remoteNodeService;
    private readonly SharedWithMeItemCounters _sharedWithMeItemCounters;

    public SharedWithMeClient(
        ILinkApiClient linkApiClient,
        IShareApiClient shareApiClient,
        IRemoteNodeService remoteNodeService,
        SharedWithMeItemCounters sharedWithMeItemCounters)
    {
        _linkApiClient = linkApiClient;
        _shareApiClient = shareApiClient;
        _remoteNodeService = remoteNodeService;
        _sharedWithMeItemCounters = sharedWithMeItemCounters;
    }

    public async IAsyncEnumerable<SharedWithMeItem?> GetSharedWithMeItemsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? anchorId = default;

        do
        {
            var response = await _shareApiClient.GetSharedWithMeItemsAsync(anchorId, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

            anchorId = response.HasMoreData ? response.AnchorId : default;

            var getItemsBlock = new TransformManyBlock<SharedWithMeItemListResponse, Contracts.SharedWithMeItem>(x => x.Items);

            var transformItemsBlock = new TransformBlock<Contracts.SharedWithMeItem, SharedWithMeItem?>(
                async x =>
                {
                    try
                    {
                        return await GetSharedWithMeItemAsync(x.ShareId, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex.IsDriveClientException())
                    {
                        var itemKey = (x.VolumeId, x.ShareId, x.LinkId);
                        _sharedWithMeItemCounters.IncrementFailures(itemKey);

                        // Do not rethrow to avoid faulting the block, which would trigger an early completion of the pipeline.
                        return null;
                    }
                },
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = MaxNumberOfParallelism, CancellationToken = cancellationToken });

            getItemsBlock.LinkTo(transformItemsBlock, new DataflowLinkOptions { PropagateCompletion = true });
            getItemsBlock.Post(response);
            getItemsBlock.Complete();

            await foreach (var item in transformItemsBlock.ReceiveAllAsync(cancellationToken))
            {
                yield return item;
            }
        }
        while (anchorId is not null);
    }

    public async Task<SharedWithMeItem?> GetSharedWithMeItemAsync(string shareId, CancellationToken cancellationToken)
    {
        var share = await _shareApiClient.GetShareAsync(shareId, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        var itemKey = (share.VolumeId, share.Id, share.LinkId);

        if (share.IsLocked
            || share.State is not ShareState.Active
            || share.Memberships.Count == 0
            || (share.Memberships[0].Permissions & MemberPermissions.Read) == 0)
        {
            return null; // Skip item
        }

        var linkResponse = await _linkApiClient.GetLinkAsync(share.Id, share.LinkId, cancellationToken).ThrowOnFailure().ConfigureAwait(false);

        var link = linkResponse.Link ?? throw new ApiException(ResponseCode.InvalidValue, $"Failed to get link with ID {share.LinkId}");

        if (link.State is not LinkState.Active)
        {
            return null; // Filter deleted item
        }

        var item = await CreateItemAsync(share, link, cancellationToken).ConfigureAwait(false);
        _sharedWithMeItemCounters.IncrementSuccesses(itemKey);
        return item;
    }

    public async Task RemoveMemberAsync(string shareId, string memberId, CancellationToken cancellationToken)
    {
        await _shareApiClient.RemoveMemberAsync(shareId, memberId, cancellationToken).ThrowOnFailure().ConfigureAwait(false);
    }

    private async Task<SharedWithMeItem?> CreateItemAsync(ShareResponse share, Link link, CancellationToken cancellationToken)
    {
        var shareRootNode = await _remoteNodeService.GetRemoteNodeAsync(share.Id, link, cancellationToken).ConfigureAwait(false);

        var membership = share.Memberships[0];

        return new SharedWithMeItem
        {
            Id = share.Id,
            Name = shareRootNode.Name,
            VolumeId = share.VolumeId,
            LinkId = share.LinkId,
            IsFolder = share.LinkType == LinkType.Folder,
            IsReadOnly = (membership.Permissions & MemberPermissions.Write) != MemberPermissions.Write,
            MemberId = membership.MemberId,
            SharingTime = DateTimeOffset.FromUnixTimeSeconds(membership.CreationTime).UtcDateTime,
            InviterEmailAddress = membership.InviterEmailAddress,
        };
    }
}
