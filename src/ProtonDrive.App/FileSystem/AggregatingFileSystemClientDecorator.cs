using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared;
using ProtonDrive.Sync.Adapter.OnDemandHydration;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem;

internal sealed class AggregatingFileSystemClientDecorator<TId> : FileSystemClientDecoratorBase<TId>
    where TId : IEquatable<TId>
{
    private readonly IRootDirectory<TId> _aggregatedRootFolder;

    private readonly DispatchingFileHydrationDemandHandler _fileHydrationDemandHandler = new();

    private int _isConnected;

    public AggregatingFileSystemClientDecorator(IRootDirectory<TId> aggregatedRootFolder, IFileSystemClient<TId> instanceToDecorate)
        : base(instanceToDecorate)
    {
        Ensure.NotNullOrEmpty(aggregatedRootFolder.Path, nameof(aggregatedRootFolder), nameof(aggregatedRootFolder.Path));

        _aggregatedRootFolder = aggregatedRootFolder;
    }

    public override void Connect(string syncRootPath, IFileHydrationDemandHandler<TId> fileHydrationDemandHandler)
    {
        syncRootPath = EnsureTrailingSeparator(syncRootPath);

        if (!syncRootPath.StartsWith(EnsureTrailingSeparator(_aggregatedRootFolder.Path), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Sync root path is outside of the aggregated root folder path", nameof(syncRootPath));
        }

        _fileHydrationDemandHandler.Add(syncRootPath, fileHydrationDemandHandler);

        if (Interlocked.CompareExchange(ref _isConnected, 1, 0) != 0)
        {
            // Already connected
            return;
        }

        base.Connect(_aggregatedRootFolder.Path, _fileHydrationDemandHandler);
    }

    public override Task DisconnectAsync()
    {
        if (Interlocked.CompareExchange(ref _isConnected, 0, 1) == 0)
        {
            // Already disconnected
            return Task.CompletedTask;
        }

        _fileHydrationDemandHandler.Clear();

        return base.DisconnectAsync();
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return !Path.EndsInDirectorySeparator(path) ? path + Path.DirectorySeparatorChar : path;
    }

    private sealed class DispatchingFileHydrationDemandHandler : IFileHydrationDemandHandler<TId>
    {
        private readonly ConcurrentDictionary<string, IFileHydrationDemandHandler<TId>> _rootPathToHandlerMap = new(StringComparer.OrdinalIgnoreCase);

        public Task HandleAsync(IFileHydrationDemand<TId> hydrationDemand, CancellationToken cancellationToken)
        {
            var folderPath = EnsureTrailingSeparator(hydrationDemand.FileInfo.Path);

            var handler = _rootPathToHandlerMap.FirstOrDefault(pair => folderPath.StartsWith(pair.Key, StringComparison.OrdinalIgnoreCase)).Value;

            if (handler == null)
            {
                throw new HydrationException("File is outside of enabled root paths");
            }

            return handler.HandleAsync(hydrationDemand, cancellationToken);
        }

        public void Add(string rootPath, IFileHydrationDemandHandler<TId> fileHydrationDemandHandler)
        {
            if (!_rootPathToHandlerMap.TryAdd(rootPath, fileHydrationDemandHandler))
            {
                throw new InvalidOperationException("File hydration demand handler for this root path already added");
            }
        }

        public void Clear()
        {
            _rootPathToHandlerMap.Clear();
        }
    }
}
