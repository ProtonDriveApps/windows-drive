using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using CommunityToolkit.HighPerformance;
using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.BlockVerification;
using ProtonDrive.Client.Configuration;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Cryptography;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client.FileUploading;

// If any cancellation occurs, the instance must be discarded as it is not fit to carry out any other operation
internal sealed class RemoteFileWriteStream : Stream
{
    public const int ThumbnailBlockIndex = 0;
    public const int DefaultBlockSize = 1 << 22;

    private const int UploadDegreeOfParallelism = 3; // Max number of blocks uploaded concurrently

    private readonly IFileApiClient _fileApiClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICryptographyService _cryptographyService;
    private readonly TransientMemoryPool<byte> _bufferPool;

    private readonly string _shareId;
    private readonly string _fileId;
    private readonly string _revisionId;
    private readonly Address _address;
    private readonly ISigningCapablePgpDataPacketProducer _encrypter;
    private readonly IThumbnailProvider _thumbnailProvider;
    private readonly IBlockVerifier _blockVerifier;
    private readonly Action<Exception> _reportBlockVerificationFailure;
    private readonly Action<Progress>? _progressCallback;
    private readonly List<UploadedBlock> _uploadedBlocks = [];

    // TODO: too many private fields, refactor pipeline as another class
    private readonly ITargetBlock<UploadCommand> _pipelineStart;
    private readonly DataflowPipeline _pipeline;
    private readonly SemaphoreSlim _pipelineThrottlingSemaphore = new(UploadDegreeOfParallelism * 2, UploadDegreeOfParallelism * 2);

    private bool _isDisposed;
    private int _thumbnailUploaded;
    private long? _length;
    private long _position;
    private int _greatestContextBlockIndexUsed;

    private IMemoryOwner<byte>? _currentContentBuffer;
    private int _currentContentBufferPosition;

    private CancellationTokenRegistration? _latestCancellationTokenRegistration;

    public RemoteFileWriteStream(
        IFileApiClient fileApiClient,
        IHttpClientFactory httpClientFactory,
        ICryptographyService cryptographyService,
        BlockingArrayMemoryPool<byte> bufferPool,
        string shareId,
        string fileId,
        string revisionId,
        Address address,
        ISigningCapablePgpDataPacketProducer encrypter,
        IThumbnailProvider thumbnailProvider,
        IBlockVerifier blockVerifier,
        Action<Exception> reportBlockVerificationFailure,
        Action<Progress>? progressCallback = null,
        int? blockSizeOverride = null)
    {
        _fileApiClient = fileApiClient;
        _httpClientFactory = httpClientFactory;
        _cryptographyService = cryptographyService;
        _bufferPool = new TransientMemoryPool<byte>(bufferPool);

        _shareId = shareId;
        _fileId = fileId;
        _revisionId = revisionId;
        _address = address;
        _encrypter = encrypter;
        _thumbnailProvider = thumbnailProvider;
        _blockVerifier = blockVerifier;
        _reportBlockVerificationFailure = reportBlockVerificationFailure;
        _progressCallback = progressCallback;

        BlockSize = blockSizeOverride ?? DefaultBlockSize;

        (_pipelineStart, _pipeline) = CreateUploadPipeline();
    }

    private enum UploadTarget
    {
        Content,
        Thumbnail,
    }

    public int BlockSize { get; }

    public IReadOnlyCollection<UploadedBlock> UploadedBlocks => _uploadedBlocks.AsReadOnly();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _length ?? 0;
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    private int EstimatedEncryptedBlockSize => BlockSize + (1 << 10);

    public override void Flush()
    {
        if (_length != null && _position < Length)
        {
            throw new InvalidOperationException("Cannot flush until all bytes have been provided.");
        }
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        Flush();
        return Task.CompletedTask;
    }

    public override void SetLength(long value)
    {
        if (_length > 0)
        {
            throw new InvalidOperationException("Length can only be set once.");
        }

        lock (_uploadedBlocks)
        {
            _uploadedBlocks.Capacity = (int)((value + BlockSize - 1) / BlockSize);
        }

        _length = value;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None).GetAwaiter().GetResult();
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_length is null)
            {
                throw new InvalidOperationException("Length must be set before writing.");
            }

            await FinishWriteAsync().ConfigureAwait(false);
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

        async ValueTask FinishWriteAsync()
        {
            await _pipeline.ThrowIfCompletedAsync().ConfigureAwait(false);

            await SendThumbnailToPipelineIfApplicableAsync(cancellationToken).ConfigureAwait(false);

            if (_position + buffer.Length > Length)
            {
                throw new IOException("Cannot write past end of stream");
            }

            var remainingBufferLength = buffer.Length;

            while (remainingBufferLength > 0)
            {
                if (_currentContentBuffer is null || _currentContentBufferPosition >= BlockSize)
                {
                    _currentContentBuffer = await _bufferPool.RentAsync(BlockSize, cancellationToken).ConfigureAwait(false);
                    _currentContentBufferPosition = 0;
                }

                var sliceToCopy = buffer.Slice(
                    buffer.Length - remainingBufferLength,
                    Math.Min(BlockSize - _currentContentBufferPosition, remainingBufferLength));

                // Null-forgiving operator is due to false positive (see above if-clause)
                sliceToCopy.CopyTo(_currentContentBuffer!.Memory[_currentContentBufferPosition..]);

                _currentContentBufferPosition += sliceToCopy.Length;

                if (_currentContentBufferPosition == BlockSize)
                {
                    await _pipeline.ThrowIfCompletedAsync().ConfigureAwait(false);

                    await SendCurrentContentBufferToPipelineAsync(cancellationToken).ConfigureAwait(false);
                }

                remainingBufferLength -= sliceToCopy.Length;
            }

            _position += buffer.Length;

            if (_position == Length)
            {
                await CompletePipelineAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

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

    private async Task SendCurrentContentBufferToPipelineAsync(CancellationToken cancellationToken)
    {
        var buffer = _currentContentBuffer;
        if (buffer is null)
        {
            throw new InvalidOperationException();
        }

        var dataLength = _currentContentBufferPosition;
        _currentContentBuffer = null;
        _currentContentBufferPosition = 0;

        await SendBufferToPipelineAsync(++_greatestContextBlockIndexUsed, buffer, dataLength, UploadTarget.Content, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task SendThumbnailToPipelineIfApplicableAsync(CancellationToken cancellationToken)
    {
        if (_position != 0)
        {
            return;
        }

        var thumbnailUploaded = Interlocked.Exchange(ref _thumbnailUploaded, 1);
        if (thumbnailUploaded != 0)
        {
            return;
        }

        const int numberOfPixelsOnLargestSide = 512;
        const int marginForEncryptionOverhead = 1 << 9;
        const int maxNumberOfBytesOnRemote = 60 * (1 << 10); // 60 KiB = 61440 B
        const int maxNumberOfBytes = maxNumberOfBytesOnRemote - marginForEncryptionOverhead;

        if (!_thumbnailProvider.TryGetThumbnail(numberOfPixelsOnLargestSide, maxNumberOfBytes, out var thumbnail))
        {
            return;
        }

        var thumbnailMemoryOwner = new ThumbnailMemoryOwner(thumbnail);

        await SendBufferToPipelineAsync(
            ThumbnailBlockIndex,
            thumbnailMemoryOwner,
            thumbnailMemoryOwner.Memory.Length,
            UploadTarget.Thumbnail,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task SendBufferToPipelineAsync(
        int index,
        IMemoryOwner<byte> buffer,
        int dataLength,
        UploadTarget target,
        CancellationToken cancellationToken)
    {
        await DisposePipelineCancellationRegistrationAsync().ConfigureAwait(false);

        _latestCancellationTokenRegistration = cancellationToken.Register(() => _pipeline.Cancel());

        var command = new UploadCommand(index, buffer, dataLength, target);
        await _pipelineStart.SendAsync(command, _pipeline.CancellationToken).ConfigureAwait(false);
    }

    private (ITargetBlock<UploadCommand> PipelineStart, DataflowPipeline Pipeline) CreateUploadPipeline()
    {
        var pipeline = new DataflowPipeline();

        var createJobBlock = new TransformBlock<UploadCommand, UploadJob>(
            input => CreateUploadJobAsync(input, pipeline.CancellationToken),
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = pipeline.CancellationToken,
                MaxDegreeOfParallelism = UploadDegreeOfParallelism,
                BoundedCapacity = UploadDegreeOfParallelism,
                SingleProducerConstrained = true,
            });

        var jobBatchBlock = new BatchBlock<UploadJob>(
            UploadDegreeOfParallelism,
            new GroupingDataflowBlockOptions { CancellationToken = pipeline.CancellationToken, BoundedCapacity = UploadDegreeOfParallelism });

        var requestUploadBlock = new TransformManyBlock<IReadOnlyCollection<UploadJob>, UploadJob>(
            async jobs =>
            {
                await RequestUploadAsync(jobs, pipeline.CancellationToken).ConfigureAwait(false);
                return jobs;
            },
            new ExecutionDataflowBlockOptions { CancellationToken = pipeline.CancellationToken, BoundedCapacity = 1, SingleProducerConstrained = true });

        var uploadBlock = new ActionBlock<UploadJob>(
            input => UploadAsync(input, pipeline.CancellationToken),
            new ExecutionDataflowBlockOptions
            {
                CancellationToken = pipeline.CancellationToken,
                /* This allows a block that's ready for upload in a batch to start being uploaded before all those of the previous batch have finished */
                EnsureOrdered = false,
                MaxDegreeOfParallelism = UploadDegreeOfParallelism,
                BoundedCapacity = UploadDegreeOfParallelism,
                SingleProducerConstrained = true,
            });

        var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };

        createJobBlock.LinkTo(jobBatchBlock, linkOptions);
        jobBatchBlock.LinkTo(requestUploadBlock, linkOptions);
        requestUploadBlock.LinkTo(uploadBlock, linkOptions);

        pipeline
            .Add(createJobBlock)
            .Add(jobBatchBlock)
            .Add(requestUploadBlock)
            .Add(uploadBlock);

        return (createJobBlock, pipeline);
    }

    private async Task<UploadJob> CreateUploadJobAsync(UploadCommand command, CancellationToken cancellationToken)
    {
        await _pipelineThrottlingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        var (encryptingStream, signatureStream) = GetEncryptingAndSignatureStreams(command);

        await using (encryptingStream.ConfigureAwait(false))
        await using (signatureStream.ConfigureAwait(false))
        {
            var encryptionBuffer = await _bufferPool.RentAsync(EstimatedEncryptedBlockSize, cancellationToken).ConfigureAwait(false);

            var blockContentLength = await encryptingStream.ReadAsync(encryptionBuffer.Memory, cancellationToken).ConfigureAwait(false);

            var blockDataPacket = encryptionBuffer.Memory[..blockContentLength];
            var plainData = command.PlainBuffer.Memory[..command.PlainDataLength];

            var verificationToken = GetVerificationToken(blockDataPacket.Span, plainData.Span, command.Index, command.Target);

            command.PlainBuffer.Dispose();

            using var signatureReader = new StreamReader(signatureStream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var signature = await signatureReader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            var blockContentStream = blockDataPacket.AsStream();
            ReadOnlyMemory<byte> hash;
            await using (blockContentStream.ConfigureAwait(false))
            {
                hash = _cryptographyService.HashBlockContent(blockContentStream);
            }

            var blockCreationParameters = new BlockCreationParameters(command.Index, blockContentLength, signature, hash, verificationToken);

            return new UploadJob(command.PlainDataLength, encryptionBuffer, blockCreationParameters, command.Target);
        }
    }

    private async Task RequestUploadAsync(IReadOnlyCollection<UploadJob> jobs, CancellationToken cancellationToken)
    {
        var jobsByTarget = jobs.ToLookup(job => job.Target);
        var thumbnailJob = jobsByTarget[UploadTarget.Thumbnail].SingleOrDefault();
        var contentJobs = jobsByTarget[UploadTarget.Content].AsReadOnlyCollection(jobs.Count - (thumbnailJob is not null ? 1 : 0));

        var blockUploadRequestParameters = new BlockUploadRequestParameters
        {
            ShareId = _shareId,
            LinkId = _fileId,
            RevisionId = _revisionId,
            AddressId = _address.Id,
            Blocks = contentJobs.Select(job => job.BlockCreationParameters),
        };

        if (thumbnailJob is not null)
        {
            blockUploadRequestParameters.IncludesThumbnail = true;
            blockUploadRequestParameters.ThumbnailHash = thumbnailJob.BlockCreationParameters.Hash;
            blockUploadRequestParameters.ThumbnailSize = thumbnailJob.BlockCreationParameters.Size;
        }

        var response = await _fileApiClient.RequestBlockUploadAsync(blockUploadRequestParameters, cancellationToken)
            .ThrowOnFailure()
            .ConfigureAwait(false);

        if (response.UploadUrls.Count != contentJobs.Count)
        {
            throw new ApiException(
                ResponseCode.InvalidValue,
                $"Block upload request returned wrong number of upload links ({response.UploadUrls.Count} instead of {contentJobs.Count})");
        }

        var i = 0;
        foreach (var job in contentJobs)
        {
            job.UploadUrl = response.UploadUrls[i++];
        }

        if (thumbnailJob is not null)
        {
            if (response.ThumbnailUrl is not { } thumbnailUrl)
            {
                throw new ApiException(ResponseCode.InvalidValue, "No thumbnail URL was returned despite the request for one.");
            }

            thumbnailJob.UploadUrl = thumbnailUrl;
        }
    }

    private async Task<UploadJob> UploadAsync(UploadJob job, CancellationToken cancellationToken)
    {
        try
        {
            using var blobContent = new ReadOnlyMemoryContent(job.EncryptionBuffer.Memory[..job.BlockCreationParameters.Size]);
            blobContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "Block", FileName = "blob" };
            blobContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

            using var multipartContent = new MultipartFormDataContent("-----------------------------" + Guid.NewGuid().ToString("N")) { blobContent };

            await PostBlockAsync(job.UploadUrl.Value, multipartContent, cancellationToken).ConfigureAwait(false);

            var uploadedBlock = new UploadedBlock(
                job.BlockCreationParameters.Index,
                job.BlockCreationParameters.Size,
                job.PlainDataLength,
                job.BlockCreationParameters.Hash,
                job.UploadUrl,
                IsThumbnail: job.Target == UploadTarget.Thumbnail);

            lock (_uploadedBlocks)
            {
                _uploadedBlocks.Add(uploadedBlock);
                _progressCallback?.Invoke(new Progress(_uploadedBlocks.Count, _uploadedBlocks.Capacity));
            }

            return job;
        }
        finally
        {
            job.EncryptionBuffer.Dispose();
            _pipelineThrottlingSemaphore.Release();
        }
    }

    private async Task PostBlockAsync(string uploadUrl, MultipartFormDataContent content, CancellationToken cancellationToken)
    {
        // TODO: remove the client creation for each block upload
        var httpClient = _httpClientFactory.CreateClient(ApiClientConfigurator.BlocksHttpClientName);

        await httpClient.PostAsync(uploadUrl, content, cancellationToken).ReadFromJsonAsync<ApiResponse>(cancellationToken).ThrowOnFailure().ConfigureAwait(false);
    }

    private ValueTask DisposePipelineCancellationRegistrationAsync()
    {
        var resultTask = _latestCancellationTokenRegistration?.DisposeAsync() ?? ValueTask.CompletedTask;
        _latestCancellationTokenRegistration = null;
        return resultTask;
    }

    private async Task CompletePipelineAsync(CancellationToken cancellationToken)
    {
        if (_currentContentBuffer is not null)
        {
            await SendCurrentContentBufferToPipelineAsync(cancellationToken).ConfigureAwait(false);
        }

        _pipelineStart.Complete();

        await _pipeline.Completion.ConfigureAwait(false);

        await DisposePipelineCancellationRegistrationAsync().ConfigureAwait(false);
    }

    private (Stream EncryptingStream, Stream SignatureStream) GetEncryptingAndSignatureStreams(UploadCommand uploadCommand)
    {
        PlainDataSource GetPlainDataSource()
        {
            return new PlainDataSource(uploadCommand.PlainBuffer.Memory[..uploadCommand.PlainDataLength].AsStream());
        }

        return uploadCommand.Target switch
        {
            UploadTarget.Content => _encrypter.GetEncryptingAndSignatureStreams(GetPlainDataSource(), DetachedSignatureParameters.ArmoredEncrypted),

            UploadTarget.Thumbnail => (_encrypter.GetEncryptingAndSigningStream(GetPlainDataSource()), Null),

            _ => throw new NotSupportedException($"Unsupported upload target \"{uploadCommand.Target}\""),
        };
    }

    private VerificationToken? GetVerificationToken(ReadOnlySpan<byte> blockDataPacket, ReadOnlySpan<byte> plainData, int blockIndex, UploadTarget target)
    {
        try
        {
            try
            {
                return target == UploadTarget.Content
                    ? _blockVerifier.VerifyBlock(blockDataPacket, plainData)
                    : default;
            }
            catch (SessionKeyAndDataPacketMismatchException ex)
            {
                throw new BlockVerificationFailedException(_shareId, _fileId, _revisionId, blockIndex, ex);
            }
        }
        catch (BlockVerificationFailedException ex)
        {
            // We report the exception only after throwing it, so that it contains the stack trace
            _reportBlockVerificationFailure.Invoke(ex);
            throw;
        }
    }

    private sealed class ThumbnailMemoryOwner : IMemoryOwner<byte>
    {
        public ThumbnailMemoryOwner(ReadOnlyMemory<byte> thumbnailBytes)
        {
            Memory = MemoryMarshal.AsMemory(thumbnailBytes);
        }

        public Memory<byte> Memory { get; }

        public void Dispose() { /* Do nothing, the Memory object is already managed */ }
    }

    private sealed record UploadCommand(int Index, IMemoryOwner<byte> PlainBuffer, int PlainDataLength, UploadTarget Target);

    private sealed class UploadJob
    {
        private UploadUrl? _uploadLink;

        public UploadJob(int plainDataLength, IMemoryOwner<byte> encryptionBuffer, BlockCreationParameters blockCreationParameters, UploadTarget target)
        {
            PlainDataLength = plainDataLength;
            EncryptionBuffer = encryptionBuffer;
            BlockCreationParameters = blockCreationParameters;
            Target = target;
        }

        public int PlainDataLength { get; }

        public IMemoryOwner<byte> EncryptionBuffer { get; }

        public BlockCreationParameters BlockCreationParameters { get; }

        public UploadUrl UploadUrl
        {
            get => _uploadLink ?? throw new InvalidOperationException();
            set => _uploadLink = value;
        }

        public UploadTarget Target { get; }
    }
}
