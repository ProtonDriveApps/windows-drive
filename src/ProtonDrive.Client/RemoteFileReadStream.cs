using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using CommunityToolkit.HighPerformance;
using Microsoft.Extensions.Logging;
using Proton.Security.Cryptography;
using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.Client.Configuration;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Client.FileUploading;
using ProtonDrive.Client.RemoteNodes;
using ProtonDrive.Client.Volumes;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.Client;

internal sealed class RemoteFileReadStream : Stream
{
    internal const int BlockPageSize = DownloadDegreeOfParallelism * 6; // Good enough for 16 KiB/s with a 30-minute expiration of download URLs
    internal const int MaxNumberOfBlocksInPipeline = DownloadDegreeOfParallelism * 2;
    internal const int MinBlockIndex = 1;

    private const int DownloadDegreeOfParallelism = 3;  // Max number of blocks downloaded concurrently

    private readonly string _volumeId;
    private readonly string _shareId;
    private readonly RemoteFile _remoteFile;
    private readonly Action<Progress>? _progressCallback;

    private readonly DriveApiConfig _config;
    private readonly IFileApiClient _fileApiClient;
    private readonly IVolumeApiClient _volumeApiClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICryptographyService _cryptographyService;
    private readonly IRevisionManifestCreator _revisionManifestCreator;
    private readonly TransientMemoryPool<byte> _bufferPool;
    private readonly ILogger<RemoteFileReadStream> _logger;
    private readonly Action<Exception> _reportBlockDecryptionFailure;

    private readonly ITargetBlock<Stream> _pipelineStart;
    private readonly DataflowPipeline _pipeline;
    private readonly SemaphoreSlim _pipelineThrottlingSemaphore = new(MaxNumberOfBlocksInPipeline, MaxNumberOfBlocksInPipeline);
    private readonly List<DownloadedBlock> _downloadedBlocks = new();

    private bool _isDisposed;
    private long _position;

    public RemoteFileReadStream(
        DriveApiConfig config,
        IFileApiClient fileApiClient,
        IVolumeApiClient volumeApiClient,
        IHttpClientFactory httpClientFactory,
        ICryptographyService cryptographyService,
        IRevisionManifestCreator revisionManifestCreator,
        BlockingArrayMemoryPool<byte> bufferPool,
        string volumeId,
        string shareId,
        RemoteFile remoteFile,
        ILogger<RemoteFileReadStream> logger,
        Action<Exception> reportBlockDecryptionFailure,
        Action<Progress>? progressCallback = default)
    {
        _config = config;
        _fileApiClient = fileApiClient;
        _volumeApiClient = volumeApiClient;
        _httpClientFactory = httpClientFactory;
        _cryptographyService = cryptographyService;
        _revisionManifestCreator = revisionManifestCreator;
        _bufferPool = new TransientMemoryPool<byte>(bufferPool);

        _volumeId = volumeId;
        _shareId = shareId;
        _remoteFile = remoteFile;
        _progressCallback = progressCallback;
        _logger = logger;
        _reportBlockDecryptionFailure = reportBlockDecryptionFailure;

        (_pipelineStart, _pipeline) = CreateDownloadPipeline();
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;

    public override long Length => _remoteFile.PlainSize ?? _remoteFile.SizeOnStorage;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override void Flush() { /* Nothing to do */ }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        CopyToAsync(destination, bufferSize).GetAwaiter().GetResult();
    }

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                await FinishCopyToAsync().ConfigureAwait(false);
            }
            catch (AggregateException ex) when (ex.Flatten().InnerException is { } innerException)
            {
                ExceptionDispatchInfo.Capture(innerException).Throw();
            }
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                // The pipeline might indicate cancellation when it is faulted.
                // Make sure the original exception that lead to the pipeline completion is raised.
                await _pipeline.ThrowIfCompletedAsync().ConfigureAwait(false);
            }

            _pipeline.Cancel();

            throw;
        }

        return;

        async Task FinishCopyToAsync()
        {
            var cancellationTokenRegistration = cancellationToken.Register(CancelPipeline);

            await using (cancellationTokenRegistration.ConfigureAwait(false))
            {
                await _pipelineStart.SendAsync(destination, _pipeline.CancellationToken).ConfigureAwait(false);

                _pipelineStart.Complete();

                await _pipeline.Completion.ConfigureAwait(false);
            }

            if (destination.CanSeek)
            {
                destination.SetLength(destination.Position);
            }
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        await _pipeline.DisposeAsync().ConfigureAwait(false);
        _bufferPool.Dispose();

        await base.DisposeAsync().ConfigureAwait(false);

        _isDisposed = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }

        _pipeline.Dispose();
        _bufferPool.Dispose();

        base.Dispose(disposing);

        _isDisposed = true;
    }

    private async Task<Stream> GetBlobStreamAsync(Block block, HttpClient httpClient, CancellationToken cancellationToken)
    {
        var blobResponse = await httpClient.GetAsync(block.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ThrowOnFailure()
            .ConfigureAwait(false);

        var blobSize = (int?)blobResponse.Content.Headers.ContentLength;

        var bufferTask = blobSize is not null
            ? _bufferPool.RentAsync(blobSize.Value, cancellationToken)
            : _bufferPool.RentAsync(cancellationToken);

        var buffer = await bufferTask.ConfigureAwait(false);

        try
        {
            var bufferStream = buffer.AsStream();
            await blobResponse.Content.CopyToAsync(bufferStream, cancellationToken).ConfigureAwait(false);

            var blobStream = buffer.Memory[..(int)bufferStream.Position].AsStream();

            return new DisposingStreamDecorator(blobStream, bufferStream);
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private static void ThrowIfNotValid(Block block)
    {
        if (string.IsNullOrEmpty(block.EncryptedSignature))
        {
            throw new ApiException(ResponseCode.InvalidValue, $"Missing signature for block blob with index {block.Index}.");
        }

        if (string.IsNullOrEmpty(block.SignatureEmailAddress))
        {
            throw new ApiException(ResponseCode.InvalidValue, $"Missing signature e-mail address for block blob with index {block.Index}.");
        }
    }

    private async ValueTask<WriteToDestinationJob> ReadDecryptedBlockAndGetNextJobAsync(
        DownloadBlockToBufferJob job,
        Stream decryptingStream,
        IMemoryOwner<byte> bufferOwner,
        CancellationToken cancellationToken)
    {
        try
        {
            var readCount = await decryptingStream.ReadAsync(bufferOwner.Memory, cancellationToken).ConfigureAwait(false);

            return new WriteToDestinationJob(job.Block.Index, job.IsLastBlock, job.Destination, bufferOwner, readCount, job.Sequencer);
        }
        catch (CryptographicException ex)
        {
            _reportBlockDecryptionFailure.Invoke(ex);
            throw;
        }
    }

    private (ITargetBlock<Stream> PipelineStart, DataflowPipeline Pipeline) CreateDownloadPipeline()
    {
        var pipeline = new DataflowPipeline();

        var downloadToBufferBlock = new TransformBlock<DownloadBlockToBufferJob, WriteToDestinationJob>(
            work => DownloadBlockToBufferAsync(work, pipeline.CancellationToken),
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = pipeline.CancellationToken,
                MaxDegreeOfParallelism = DownloadDegreeOfParallelism,
                BoundedCapacity = DownloadDegreeOfParallelism,
                EnsureOrdered = false,
                SingleProducerConstrained = true,
            });

        var createBlockDownloadJobsBlock = new ActionBlock<Stream>(
            stream => CreateDownloadBlockToBufferJobs(stream, downloadToBufferBlock, pipeline.CancellationToken),
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = pipeline.CancellationToken,
                SingleProducerConstrained = true,
            });

        var reorderingBlock = new TransformManyBlock<WriteToDestinationJob, WriteToDestinationJob>(
            job => job.Sequencer.GetNextAvailableSequenceSegment(job));

        var writeToDestinationBlock = new ActionBlock<WriteToDestinationJob>(
            work => WriteBlockToDestinationAsync(work, pipeline.CancellationToken),
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = pipeline.CancellationToken,
                SingleProducerConstrained = true,
            });

        var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

        downloadToBufferBlock.LinkTo(reorderingBlock, linkOptions);
        reorderingBlock.LinkTo(writeToDestinationBlock, linkOptions);

        createBlockDownloadJobsBlock.Completion.ContinueWith(_ => downloadToBufferBlock.Complete());

        pipeline
            .Add(createBlockDownloadJobsBlock)
            .Add(downloadToBufferBlock)
            .Add(reorderingBlock)
            .Add(writeToDestinationBlock);

        return (createBlockDownloadJobsBlock, pipeline);
    }

    private async Task CreateDownloadBlockToBufferJobs(
        Stream destination,
        ITargetBlock<DownloadBlockToBufferJob> downloadToBufferBlock,
        CancellationToken cancellationToken)
    {
        var activeRevision = _remoteFile.ActiveRevision
            ?? throw new ApiException(ResponseCode.InvalidValue, "The specified file has no active revision to download.");

        var sequencer = new MessageSequencer<WriteToDestinationJob>(message => message.Index);

        if (destination.CanSeek && destination.Length < Length)
        {
            destination.SetLength(Length);
        }

        await DownloadThumbnailBlocksAsync(activeRevision, cancellationToken).ConfigureAwait(false);

        var blocks = GetBlocksAsync(activeRevision.Id, cancellationToken);

        await foreach (var (block, isLast) in blocks)
        {
            await SendBlockToDownloadPipelineAsync(block, isLast, destination, downloadToBufferBlock, sequencer, cancellationToken).ConfigureAwait(false);
        }
    }

    private async IAsyncEnumerable<(Block Value, bool IsLast)> GetBlocksAsync(string revisionId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var mustTryNextPageOfBlocks = true;
        var lastKnownIndex = MinBlockIndex - 1;
        var outstandingBlock = default(Block);
        var currentPageSortedBlocks = new SortedList<int, Block>(BlockPageSize);

        while (mustTryNextPageOfBlocks)
        {
            currentPageSortedBlocks.Clear();

            var revisionResponse =
                await _fileApiClient.GetRevisionAsync(
                        _shareId,
                        _remoteFile.Id,
                        revisionId,
                        lastKnownIndex + 1,
                        BlockPageSize,
                        false,
                        cancellationToken)
                    .ThrowOnFailure().ConfigureAwait(false);

            var revision = revisionResponse.Revision
                ?? throw new ApiException(ResponseCode.InvalidValue, "Failed to get list of blocks for active revision.");

            cancellationToken.ThrowIfCancellationRequested();

            if (revision.Blocks.Count == 0)
            {
                break;
            }

            mustTryNextPageOfBlocks = revision.Blocks.Count >= BlockPageSize;

            foreach (var block in revision.Blocks)
            {
                currentPageSortedBlocks[block.Index] = block;
                lastKnownIndex = Math.Max(lastKnownIndex, block.Index);
            }

            var blocksExceptLast = currentPageSortedBlocks.Values.Take(currentPageSortedBlocks.Count - 1);
            var blocksToReturn = outstandingBlock is not null ? blocksExceptLast.Prepend(outstandingBlock) : blocksExceptLast;

            outstandingBlock = currentPageSortedBlocks[lastKnownIndex];

            foreach (var block in blocksToReturn)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return (block, false);
            }
        }

        if (outstandingBlock is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return (outstandingBlock, true);
        }
    }

    private async Task SendBlockToDownloadPipelineAsync(
        Block block,
        bool isLast,
        Stream destination,
        ITargetBlock<DownloadBlockToBufferJob> downloadToBufferBlock,
        MessageSequencer<WriteToDestinationJob> sequencer,
        CancellationToken cancellationToken)
    {
        ThrowIfNotValid(block);

        sequencer.AddIndex(block.Index);

        var httpClient = _httpClientFactory.CreateClient(ApiClientConfigurator.BlocksHttpClientName);

        await _pipelineThrottlingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var job = new DownloadBlockToBufferJob(block, isLast, destination, httpClient, sequencer);
            await downloadToBufferBlock.SendAsync(job, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _pipelineThrottlingSemaphore.Release();
            throw;
        }
    }

    private async Task<WriteToDestinationJob> DownloadBlockToBufferAsync(DownloadBlockToBufferJob job, CancellationToken cancellationToken)
    {
        try
        {
            var bufferOwner = await _bufferPool.RentAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await DownloadBlockToBufferWithRetriesAsync(job, bufferOwner, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                bufferOwner.Dispose();
                throw;
            }
        }
        catch
        {
            _pipelineThrottlingSemaphore.Release();
            throw;
        }
    }

    private async Task<WriteToDestinationJob> DownloadBlockToBufferWithRetriesAsync(
        DownloadBlockToBufferJob job,
        IMemoryOwner<byte> bufferOwner,
        CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (true)
        {
            try
            {
                return await InternalDownloadBlockToBufferAsync(job, bufferOwner, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException ex) when (attempt++ <= _config.DriveApiNumberOfRetries)
            {
                _logger.LogInformation(
                    "The block with index={Index} failed to download. Retry {Attempt}/{MaxNumberOfAttempts}: {Message}",
                    job.Block.Index,
                    attempt,
                    _config.DriveApiNumberOfRetries,
                    ex.Message);

                var delay = TimeSpan.FromSeconds(Math.Pow(2.5, attempt) / 2);
                await Task.Delay(delay.RandomizedWithDeviation(0.2), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<WriteToDestinationJob> InternalDownloadBlockToBufferAsync(
        DownloadBlockToBufferJob job,
        IMemoryOwner<byte> bufferOwner,
        CancellationToken cancellationToken)
    {
        var decrypter = await _cryptographyService.CreateFileContentsBlockDecrypterAsync(
            _remoteFile.PrivateKey,
            job.Block.SignatureEmailAddress,
            cancellationToken).ConfigureAwait(false);

        var blobStream = await GetBlobStreamAsync(job.Block, job.HttpClient, cancellationToken).ConfigureAwait(false);

        try
        {
            await using (blobStream.ConfigureAwait(false))
            {
                var hash = _cryptographyService.HashBlockContent(blobStream);

                lock (_downloadedBlocks)
                {
                    _downloadedBlocks.Add(new(job.Block.Index, hash));
                }

                blobStream.Seek(0, SeekOrigin.Begin);

                var contentMessageStream = new ConcatenatingStream(_remoteFile.ContentKeyPacket.AsStream(), blobStream);
                var pgpMessageSource = new PgpMessageSource(contentMessageStream);
                var pgpSignatureSource = new PgpMessageSource(new AsciiStream(job.Block.EncryptedSignature ?? string.Empty), PgpArmoring.Ascii);
                await using (pgpMessageSource.ConfigureAwait(false))
                await using (pgpSignatureSource.ConfigureAwait(false))
                {
                    var (decryptingStream, verificationTask) = decrypter.GetDecryptingAndVerifyingStream(pgpMessageSource, pgpSignatureSource);

                    await using (decryptingStream.ConfigureAwait(false))
                    {
                        var writeToDestinationJob = await ReadDecryptedBlockAndGetNextJobAsync(job, decryptingStream, bufferOwner, cancellationToken)
                            .ConfigureAwait(false);

                        LogIfBlockSignatureIsInvalid(verificationTask, job.Block.Index);

                        return writeToDestinationJob;
                    }
                }
            }
        }
        finally
        {
            await blobStream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task WriteBlockToDestinationAsync(WriteToDestinationJob job, CancellationToken cancellationToken)
    {
        try
        {
            if (job.IsLastBlock)
            {
                await CheckIntegrityAsync(cancellationToken).ConfigureAwait(false);
            }

            await job.Destination.WriteAsync(job.Buffer.Memory[..job.NumberOfBytesToWrite], cancellationToken).ConfigureAwait(false);
            _position += job.NumberOfBytesToWrite;

            _progressCallback?.Invoke(new Progress(_position, Length));
        }
        finally
        {
            job.Buffer.Dispose();
            _pipelineThrottlingSemaphore.Release();
        }
    }

    private async Task DownloadThumbnailBlocksAsync(RevisionHeader activeRevision, CancellationToken cancellationToken)
    {
        if (activeRevision.Thumbnails.Count == 0)
        {
            return;
        }

        var thumbnailsByIds = activeRevision.Thumbnails.ToDictionary(x => x.Id);

        var parameters = new ThumbnailQueryParameters { ThumbnailIds = thumbnailsByIds.Keys };
        var thumbnailListResponse = await _volumeApiClient.GetThumbnailsAsync(_volumeId, parameters, cancellationToken)
            .ThrowOnFailure()
            .ConfigureAwait(false);

        await Parallel.ForEachAsync(
            thumbnailListResponse.Thumbnails,
            cancellationToken,
            async (thumbnail, ct) =>
            {
                var httpClient = _httpClientFactory.CreateClient(ApiClientConfigurator.BlocksHttpClientName);
                var thumbnailUrl = Path.Join(thumbnail.BareUrl, "/", thumbnail.Token);
                var response = await httpClient.GetAsync(thumbnailUrl, ct).ThrowOnFailure().ConfigureAwait(false);
                var thumbnailBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                var hash = _cryptographyService.HashBlockContent(thumbnailBytes.AsMemory().AsStream());

                // Fake block index used to order the thumbnails before the content blocks, for the manifest generation
                var blockIndex = int.MinValue + thumbnailsByIds[thumbnail.Id].Type;

                lock (_downloadedBlocks)
                {
                    _downloadedBlocks.Add(new(blockIndex, hash));
                }
            })
            .ConfigureAwait(false);
    }

    private async Task CheckIntegrityAsync(CancellationToken cancellationToken)
    {
        var activeRevision = _remoteFile.ActiveRevision
            ?? throw new ApiException(ResponseCode.InvalidValue, "The specified file has no active revision.");

        ReadOnlyMemory<byte> manifest;

        lock (_downloadedBlocks)
        {
            manifest = _revisionManifestCreator.CreateManifest(_downloadedBlocks);
        }

        var verificationVerdict =
            await _cryptographyService.VerifyManifestAsync(manifest, activeRevision.ManifestSignature, activeRevision.SignatureEmailAddress, cancellationToken)
                .ConfigureAwait(false);

        if (verificationVerdict != VerificationVerdict.ValidSignature)
        {
            _logger.Log(
                LogLevel.Warning,
                "Manifest verification failure for file with link ID {LinkId}: {VerificationVerdict}",
                _remoteFile.Id,
                verificationVerdict);
        }
    }

    private void CancelPipeline()
    {
        _pipeline.Cancel();
    }

    private void LogIfBlockSignatureIsInvalid(Task<VerificationVerdict> task, int blockIndex)
    {
        // TODO: Instead of Task<>, use a type that expresses the guarantee of a result
        Trace.Assert(task.IsCompleted, "Signature verification task is not completed");

        var code = task.Result;
        if (code == VerificationVerdict.ValidSignature)
        {
            return;
        }

        // TODO: pass the verification failure as result for marking nodes as suspicious.
        _logger.LogWarning(
            "Signature problem on block at index {BlockIndex} of file with ID {FileId}: {VerificationResultCode}",
            blockIndex,
            _remoteFile.Id,
            code);
    }

    private sealed record DownloadBlockToBufferJob(
        Block Block,
        bool IsLastBlock,
        Stream Destination,
        HttpClient HttpClient,
        MessageSequencer<WriteToDestinationJob> Sequencer);

    private sealed record WriteToDestinationJob(
        int Index,
        bool IsLastBlock,
        Stream Destination,
        IMemoryOwner<byte> Buffer,
        int NumberOfBytesToWrite,
        MessageSequencer<WriteToDestinationJob> Sequencer);

    private sealed class MessageSequencer<T>
    {
        private readonly Func<T, int> _getIndexFunction;
        private readonly SortedDictionary<int, T> _pendingMessagesBuffer = new();
        private readonly Queue<int> _indexQueue = new();

        public MessageSequencer(Func<T, int> getIndexFunction)
        {
            _getIndexFunction = getIndexFunction;
        }

        public void AddIndex(int index)
        {
            lock (_indexQueue)
            {
                _indexQueue.Enqueue(index);
            }
        }

        /// <summary>
        /// If the given message is the next expected in the sequence, provides a segment of the sequence that starts with the given message and continues
        /// with as many consecutive messages that are available in the buffer, otherwise stores the given message in the buffer for later use.
        /// </summary>
        /// <param name="message">A message within the sequence</param>
        /// <returns>A segment of the sequence if the given message makes one available, otherwise an empty enumerable</returns>
        public IEnumerable<T> GetNextAvailableSequenceSegment(T message)
        {
            if (!TryDequeueMessageIndex(message))
            {
                yield break;
            }

            yield return message;

            while (TryDequeuePendingMessageIndex(out var pendingMessage))
            {
                yield return pendingMessage;
            }
        }

        private bool TryDequeueMessageIndex(T message)
        {
            var index = _getIndexFunction.Invoke(message);

            lock (_indexQueue)
            {
                if (index != _indexQueue.Peek())
                {
                    _pendingMessagesBuffer.Add(index, message);
                    return false;
                }

                _indexQueue.Dequeue();
            }

            return true;
        }

        private bool TryDequeuePendingMessageIndex([MaybeNullWhen(false)] out T message)
        {
            lock (_indexQueue)
            {
                if (!_indexQueue.TryPeek(out var nextIndex))
                {
                    message = default;
                    return false;
                }

                if (!_pendingMessagesBuffer.TryGetValue(nextIndex, out message))
                {
                    return false;
                }

                _pendingMessagesBuffer.Remove(_indexQueue.Dequeue());

                return true;
            }
        }
    }
}
