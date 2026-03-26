using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Adapter;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.App.Sync;

internal sealed class FileUploadAbortionStrategy(ILogger<FileUploadAbortionStrategy> logger) : IFileTransferAbortionStrategy<long>
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

        lock (cancellationTokenSource)
        {
            cancellationTokenSource.Dispose();
        }
    }

    public void HandleFileChanged(LooseCompoundAltIdentity<long> altId)
    {
        if (!_fileTransferCancellationTokenSources.TryGetValue(altId, out var cancellationTokenSource))
        {
            return;
        }

        lock (cancellationTokenSource)
        {
            if (!cancellationTokenSource.IsCancellationRequested)
            {
                logger.LogWarning("File change detected for {ExternalId}", altId);
            }

            try
            {
                cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // File closed
            }
        }
    }
}
