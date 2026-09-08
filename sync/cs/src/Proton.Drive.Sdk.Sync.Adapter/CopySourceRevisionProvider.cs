using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Adapter.NodeCopying;
using Proton.Drive.Sdk.Sync.Adapter.Trees.Adapter;
using Proton.Drive.Sdk.Sync.Shared.Adapters;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Adapter;

internal sealed class CopySourceRevisionProvider<TId, TAltId> : ICopySourceRevisionProvider<TId, TAltId>
    where TId : struct, IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly ILogger<CopySourceRevisionProvider<TId, TAltId>> _logger;
    private readonly IScheduler _syncScheduler;
    private readonly ICopiedNodes<TId, TAltId> _copiedNodes;
    private readonly IFileRevisionProvider<TId> _externalFileRevisionProvider;
    private readonly IMappedNodeIdentityProvider<TId> _mappedNodeIdProvider;

    public CopySourceRevisionProvider(
        ILogger<CopySourceRevisionProvider<TId, TAltId>> logger,
        IScheduler syncScheduler,
        ICopiedNodes<TId, TAltId> copiedNodes,
        IFileRevisionProvider<TId> externalFileRevisionProvider,
        IMappedNodeIdentityProvider<TId> mappedNodeIdProvider)
    {
        _logger = logger;
        _syncScheduler = syncScheduler;
        _copiedNodes = copiedNodes;
        _externalFileRevisionProvider = externalFileRevisionProvider;
        _mappedNodeIdProvider = mappedNodeIdProvider;
    }

    public async Task<ISourceRevision?> OpenForReadingAsync(TId id, CancellationToken cancellationToken)
    {
        var fallbackNodeModel = await Schedule(() => GetFallbackNodeModelOrDefault(id), cancellationToken).ConfigureAwait(false);

        if (fallbackNodeModel is null)
        {
            _logger.LogDebug("Adapter Tree file node with Id={Id} has no link to source of copying", id);

            return null;
        }

        _logger.LogInformation("Reading the file with Id={Id} redirected to the copy source with Id={SourceId}", id, fallbackNodeModel.Id);

        var mappedFallbackNodeId = await GetMappedNodeIdOrDefaultAsync(fallbackNodeModel.Id, cancellationToken).ConfigureAwait(false);

        if (IsDefault(mappedFallbackNodeId))
        {
            _logger.LogWarning("The copy source file with Id={Id} is not mapped", fallbackNodeModel.Id);

            return null;
        }

        // The content version on the fallback node can have different value, because when the file was moved to a folder belonging to
        // different move scope, a destination node content version got a new unique value. If the folder was moved, ancestor file nodes
        // retain original content version values.
        return await _externalFileRevisionProvider.OpenFileForReadingAsync(mappedFallbackNodeId.Value, fallbackNodeModel.ContentVersion, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsDefault([NotNullWhen(false)] TId? value)
    {
        // TId? only exposes Equals(object), so the argument must be explicitly typed,
        // otherwise a bare default binds to object and is null.
        return value is null || value.Equals(default(TId));
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
