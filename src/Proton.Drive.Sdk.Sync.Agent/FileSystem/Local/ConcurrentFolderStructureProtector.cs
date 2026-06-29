using DotNext.Threading;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Shared;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Local;

internal sealed class ConcurrentFolderStructureProtector<TKey>
    where TKey : notnull
{
    private readonly ISyncFolderStructureProtector _folderStructureProtector;

    private readonly AsyncExclusiveLock _folderProtectionLock = new();
    private readonly Dictionary<TKey, int> _folderReferenceCounters = new();

    public ConcurrentFolderStructureProtector(ISyncFolderStructureProtector folderStructureProtector)
    {
        _folderStructureProtector = folderStructureProtector;
    }

    public async Task<IAsyncDisposable> UnprotectFolderAsync(TKey key, string folderPath, CancellationToken cancellationToken)
    {
        using (await AsyncLock.AcquireLockAsync(_folderProtectionLock, cancellationToken).ConfigureAwait(false))
        {
            if (_folderReferenceCounters.TryGetValue(key, out var refCount))
            {
                refCount++;
                _folderReferenceCounters[key] = refCount;
            }
            else
            {
                _folderReferenceCounters[key] = 1;
                UnprotectFolder(folderPath);
            }
        }

        return new AsyncDisposable(async () =>
        {
            // Disposal should be handled regardless of cancellation
            using (await AsyncLock.AcquireLockAsync(_folderProtectionLock, CancellationToken.None).ConfigureAwait(false))
            {
                if (_folderReferenceCounters.TryGetValue(key, out var refCount))
                {
                    refCount--;

                    if (refCount == 0)
                    {
                        _folderReferenceCounters.Remove(key);
                        ProtectFolder(folderPath);
                    }
                    else
                    {
                        _folderReferenceCounters[key] = refCount;
                    }
                }
            }
        });
    }

    private void ProtectFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            return;
        }

        _folderStructureProtector.ProtectFolder(folderPath, FolderProtectionType.ReadOnly);
    }

    private void UnprotectFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            return;
        }

        _folderStructureProtector.UnprotectFolder(folderPath, FolderProtectionType.ReadOnly);
    }
}
