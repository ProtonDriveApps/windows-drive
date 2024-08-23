using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Adapter.NodeCopying;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Adapters;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Adapter;

internal sealed class FallbackFileRevisionProviderDecorator<TId, TAltId> : IFileRevisionProvider<TId>
    where TId : struct, IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ITransactedScheduler _syncScheduler;
    private readonly ICopiedNodes<TId, TAltId> _copiedNodes;
    private readonly IFileRevisionProvider<TId> _externalFileRevisionProvider;
    private readonly IMappedNodeIdentityProvider<TId> _mappedNodeIdProvider;
    private readonly ILogger<FallbackFileRevisionProviderDecorator<TId, TAltId>> _logger;
    private readonly IFileRevisionProvider<TId> _decoratedInstance;

    public FallbackFileRevisionProviderDecorator(
        ILogger<FallbackFileRevisionProviderDecorator<TId, TAltId>> logger,
        ITransactedScheduler syncScheduler,
        ICopiedNodes<TId, TAltId> copiedNodes,
        IFileRevisionProvider<TId> externalFileRevisionProvider,
        IMappedNodeIdentityProvider<TId> mappedNodeIdProvider,
        IFileRevisionProvider<TId> decoratedInstance)
    {
        _logger = logger;
        _syncScheduler = syncScheduler;
        _copiedNodes = copiedNodes;
        _externalFileRevisionProvider = externalFileRevisionProvider;
        _mappedNodeIdProvider = mappedNodeIdProvider;
        _decoratedInstance = decoratedInstance;
    }

    public async Task<IRevision> OpenFileForReadingAsync(TId id, long version, CancellationToken cancellationToken)
    {
        try
        {
            return await _decoratedInstance.OpenFileForReadingAsync(id, version, cancellationToken).ConfigureAwait(false);
        }
        catch (FileRevisionProviderException ex) when (ex.ErrorCode is FileSystemErrorCode.Partial)
        {
            // If the file is partial, it could be due to move was replaced with copying and deletion
            var result = await OpenFallbackFileForReadingAsync(id, cancellationToken).ConfigureAwait(false);

            if (result is null)
            {
                throw;
            }

            return result;
        }
    }

    private static bool IsDefault([NotNullWhen(false)] TId? value)
    {
        return value is null || value.Equals(default);
    }

    private async Task<IRevision?> OpenFallbackFileForReadingAsync(TId id, CancellationToken cancellationToken)
    {
        var fallbackNodeModel = await Schedule(() => GetFallbackNodeModelOrDefault(id), cancellationToken).ConfigureAwait(false);

        if (fallbackNodeModel is null)
        {
            _logger.LogDebug("Adapter Tree file node with Id={Id} has no link to source of copying", id);

            return default;
        }

        _logger.LogInformation("Reading the file with Id={Id} redirected to Id={FallbackId}", id, fallbackNodeModel.Id);

        var mappedFallbackNodeId = await GetMappedNodeIdOrDefaultAsync(fallbackNodeModel.Id, cancellationToken).ConfigureAwait(false);

        if (IsDefault(mappedFallbackNodeId))
        {
            _logger.LogWarning("The fallback file with Id={Id} is not mapped", fallbackNodeModel.Id);

            return default;
        }

        // The content version on the fallback node can have different value, because when the file was moved to a folder belonging to
        // different move scope, a destination node content version got a new unique value. If the folder was moved, ancestor file nodes
        // retain original content version values.
        return await _externalFileRevisionProvider.OpenFileForReadingAsync(mappedFallbackNodeId.Value, fallbackNodeModel.ContentVersion, cancellationToken).ConfigureAwait(false);
    }

    private AdapterTreeNodeModel<TId, TAltId>? GetFallbackNodeModelOrDefault(TId id)
    {
        return _copiedNodes.GetSourceNodeOrDefault(id)?.Model;
    }

    private Task<TId?> GetMappedNodeIdOrDefaultAsync(TId nodeId, CancellationToken cancellationToken)
    {
        return _mappedNodeIdProvider.GetMappedNodeIdOrDefaultAsync(nodeId, cancellationToken);
    }

    private Task<T> Schedule<T>(Func<T> origin, CancellationToken cancellationToken)
    {
        return _syncScheduler.Schedule(origin, cancellationToken);
    }
}
