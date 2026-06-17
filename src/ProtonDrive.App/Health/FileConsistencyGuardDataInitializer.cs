using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MoreLinq;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Adapter.Trees.Adapter;
using ProtonDrive.Sync.DataAccess.Databases;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.Health;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.App.Health;

internal sealed class FileConsistencyGuardDataInitializer
{
    private readonly FileConsistencyGuardDatabase _database;
    private readonly ITransactedScheduler _localAdapterSyncScheduler;
    private readonly LocalAdapterDatabase _localAdapterDatabase;
    private readonly ITransactedScheduler _remoteAdapterSyncScheduler;
    private readonly RemoteAdapterDatabase _remoteAdapterDatabase;
    private readonly ILogger<FileConsistencyGuardDataInitializer> _logger;

    public FileConsistencyGuardDataInitializer(
        FileConsistencyGuardDatabase database,
        ITransactedScheduler localAdapterSyncScheduler,
        LocalAdapterDatabase localAdapterDatabase,
        ITransactedScheduler remoteAdapterSyncScheduler,
        RemoteAdapterDatabase remoteAdapterDatabase,
        ILogger<FileConsistencyGuardDataInitializer> logger)
    {
        _database = database;
        _localAdapterSyncScheduler = localAdapterSyncScheduler;
        _localAdapterDatabase = localAdapterDatabase;
        _remoteAdapterSyncScheduler = remoteAdapterSyncScheduler;
        _remoteAdapterDatabase = remoteAdapterDatabase;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("File consistency guard: Initializing");
        var startTimestamp = Stopwatch.GetTimestamp();

        var processedFiles = await InitializeDataAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "File consistency guard: Initialized {NumberOfFiles} files in {ElapsedTime}",
            processedFiles,
            Stopwatch.GetElapsedTime(startTimestamp));

        return true;
    }

    private async ValueTask<int> InitializeDataAsync(CancellationToken cancellationToken)
    {
        await ClearDatabaseAsync().ConfigureAwait(false);

        var localFileNodes = await GetLocalFileNodesAsync(cancellationToken).ConfigureAwait(false);

        var remoteFileNodes = await GetRemoteFileNodesAsync(cancellationToken).ConfigureAwait(false);

        var skippedFiles = 0;

        using var transaction = _database.BeginTransaction();

        foreach (var (localRootId, localNode) in localFileNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!remoteFileNodes.TryGetValue(localNode.Id, out var remoteItem))
            {
                skippedFiles++;
                continue;
            }

            var (remoteRootId, remoteNode) = remoteItem;

            var file = new FileConsistencyGuardFileModel
            {
                Id = localNode.Id,
                LocalRootId = localRootId,
                RemoteRootId = remoteRootId,
                LocalId = localNode.AltId.ItemId,
                RemoteId = remoteNode.AltId.ItemId ?? throw new InvalidOperationException(),
                Name = localNode.Name,
                LocalSize = localNode.Size,
                RemoteSize = remoteNode.Size,
                LocalLastWriteTime = localNode.LastWriteTime,
                RemoteLastWriteTime = remoteNode.LastWriteTime,
                RevisionId = remoteNode.RevisionId,
                ContentVersion = localNode.ContentVersion,
            };

            file.UpdateStatus(remoteNode.ContentVersion);

            await _database.FileRepository.AddFileAsync(file).ConfigureAwait(false);
        }

        transaction.Commit();

        if (skippedFiles != 0)
        {
            _logger.LogInformation("File consistency guard: Skipped {NumberOfFiles} files without remote node", skippedFiles);
        }

        return localFileNodes.Count - skippedFiles;
    }

    private async ValueTask ClearDatabaseAsync()
    {
        using var transaction = _database.BeginTransaction();

        await _database.FileRepository.ClearFilesAsync().ConfigureAwait(false);

        transaction.Commit();
    }

    private async Task<IReadOnlyList<(int RootId, AdapterTreeNodeModel<long, long> Node)>> GetLocalFileNodesAsync(CancellationToken cancellationToken)
    {
        using (await _localAdapterSyncScheduler.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            _localAdapterSyncScheduler.ForceCommit = true;

            return GetLocalFileNodes().ToList();
        }

        IEnumerable<(int RootId, AdapterTreeNodeModel<long, long> Node)> GetLocalFileNodes()
        {
            var rootNode = _localAdapterDatabase.AdapterTreeRepository.NodeById(0);
            if (rootNode is null)
            {
                yield break;
            }

            var syncRootNodes = _localAdapterDatabase.AdapterTreeRepository.Children(rootNode);

            foreach (var syncRootNode in syncRootNodes)
            {
                if (syncRootNode.Type is not NodeType.Directory)
                {
                    continue;
                }

                if (!int.TryParse(syncRootNode.Name, out var rootId))
                {
                    throw new InvalidOperationException($"Failed to parse sync root ID from name \"{syncRootNode.Name}\"");
                }

                var fileNodes = MoreEnumerable
                    .TraverseDepthFirst(syncRootNode, parentNode => _localAdapterDatabase.AdapterTreeRepository.Children(parentNode))
                    .Where(node =>
                        node.Type is NodeType.File
                        && !node.AltId.IsDefault()
                        && node.AltId.ItemId != 0
                        && !node.IsDirtyPlaceholder());

                foreach (var fileNode in fileNodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    yield return (rootId, fileNode);
                }
            }
        }
    }

    private async Task<IReadOnlyDictionary<long, (int RootId, AdapterTreeNodeModel<long, string> Node)>> GetRemoteFileNodesAsync(CancellationToken cancellationToken)
    {
        using (await _remoteAdapterSyncScheduler.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            _remoteAdapterSyncScheduler.ForceCommit = true;

            return GetRemoteFileNodes().ToDictionary(x => x.Node.Id, x => x);
        }

        IEnumerable<(int RootId, AdapterTreeNodeModel<long, string> Node)> GetRemoteFileNodes()
        {
            var rootNode = _remoteAdapterDatabase.AdapterTreeRepository.NodeById(0);
            if (rootNode is null)
            {
                yield break;
            }

            var syncRootNodes = _remoteAdapterDatabase.AdapterTreeRepository.Children(rootNode);

            foreach (var syncRootNode in syncRootNodes)
            {
                if (syncRootNode.Type is not NodeType.Directory)
                {
                    continue;
                }

                if (!int.TryParse(syncRootNode.Name, out var rootId))
                {
                    throw new InvalidOperationException($"Failed to parse sync root ID from name \"{syncRootNode.Name}\"");
                }

                var fileNodes = MoreEnumerable
                    .TraverseDepthFirst(syncRootNode, parentNode => _remoteAdapterDatabase.AdapterTreeRepository.Children(parentNode))
                    .Where(node =>
                        node.Type is NodeType.File
                        && !node.AltId.IsDefault()
                        && !string.IsNullOrEmpty(node.AltId.ItemId)
                        && !node.IsDirtyPlaceholder());

                foreach (var fileNode in fileNodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    yield return (rootId, fileNode);
                }
            }
        }
    }
}
