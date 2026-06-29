using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Adapter.OperationExecution;

internal static class RevisionCopyingExtensions
{
    public static async Task CopyContentToAsync<TId>(this ISourceRevision sourceRevision, IDestinationRevision<TId> destinationRevision, FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
        where TId : IEquatable<TId>
    {
        if (sourceRevision.CanGetContentStream)
        {
            // File upload, because remote file revision does not expose content stream
            await destinationRevision.WriteContentAsync(sourceRevision.GetContentStream(), expectedChecksum, cancellationToken).ConfigureAwait(false);
        }
        else if (destinationRevision.CanGetContentStream)
        {
            // File download, because local file revision always exposes content stream
            await sourceRevision.CopyContentToAsync(destinationRevision.GetContentStream(), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Copying one remote file to another remote file
            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var invertingStream = new InvertingStream();
            await using (invertingStream.ConfigureAwait(false))
            {
                var readingTask = CopySourceRevisionAsync(sourceRevision, invertingStream, cancellationTokenSource.Token);
                var writingTask = destinationRevision.WriteContentAsync(invertingStream, expectedChecksum, cancellationTokenSource.Token);

                await foreach (var completedTask in Task.WhenEach(readingTask, writingTask).WithCancellation(CancellationToken.None).ConfigureAwait(false))
                {
                    if (!completedTask.IsCompletedSuccessfully)
                    {
                        await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
                    }
                }

                await Task.WhenAll(readingTask, writingTask).ConfigureAwait(false);
            }
        }
    }

    private static async Task CopySourceRevisionAsync(ISourceRevision sourceRevision, InvertingStream invertingStream, CancellationToken cancellationToken)
    {
        try
        {
            await sourceRevision.CopyContentToAsync(invertingStream, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            invertingStream.CompleteWriting();
        }
    }
}
