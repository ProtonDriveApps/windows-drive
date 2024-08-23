using System.Collections.Concurrent;
using System.Threading;
using ProtonDrive.Sync.Adapter;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.App.Sync;

internal sealed class FileUploadAbortionStrategy : IFileTransferAbortionStrategy<long>
{
    private readonly ConcurrentDictionary<LooseCompoundAltIdentity<long>, CancellationTokenSource> _fileTransferCancellationTokenSources = new();

    public CancellationToken HandleFileOpenedForReading(LooseCompoundAltIdentity<long> altId)
    {
        var cancellationTokenSource = new CancellationTokenSource();

        _fileTransferCancellationTokenSources.AddOrUpdate(
            altId,
            cancellationTokenSource,
            (_, existingCancellationTokenSource) =>
            {
                existingCancellationTokenSource.Cancel();
                existingCancellationTokenSource.Dispose();

                return cancellationTokenSource;
            });

        return cancellationTokenSource.Token;
    }

    public void HandleFileClosed(LooseCompoundAltIdentity<long> altId)
    {
        if (!_fileTransferCancellationTokenSources.TryRemove(altId, out var cancellationTokenSource))
        {
            return;
        }

        cancellationTokenSource.Dispose();
    }

    public void HandleFileChanged(LooseCompoundAltIdentity<long> altId)
    {
        if (!_fileTransferCancellationTokenSources.TryGetValue(altId, out var cancellationTokenSource))
        {
            return;
        }

        cancellationTokenSource.Cancel();
    }
}
