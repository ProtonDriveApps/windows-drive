using Proton.Drive.Sdk.Nodes.Upload;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client.FileUploading;

internal sealed class SdkRemoteRevisionCreationProcess : IRevisionCreationProcess<string>
{
    private readonly FileUploader _fileUploader;
    private readonly IThumbnailProvider _thumbnailProvider;
    private readonly Action<Progress>? _progressCallback;
    private readonly Action<Exception> _reportIntegrityFailure;

    public SdkRemoteRevisionCreationProcess(
        FileUploader fileUploader,
        NodeInfo<string> fileInfo,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        Action<Exception> reportIntegrityFailure)
    {
        FileInfo = fileInfo;
        _fileUploader = fileUploader;
        _thumbnailProvider = thumbnailProvider;
        _progressCallback = progressCallback;
        _reportIntegrityFailure = reportIntegrityFailure;
    }

    public NodeInfo<string> FileInfo { get; private set; }
    public NodeInfo<string> BackupInfo { get; set; } = NodeInfo<string>.Empty();
    public bool ImmediateHydrationRequired => true;
    public bool CanGetContentStream => false;

    public Stream GetContentStream()
    {
        throw new NotSupportedException();
    }

    public async Task WriteContentAsync(Stream contentStream, CancellationToken cancellationToken)
    {
        var thumbnails = await _thumbnailProvider.GetThumbnailsAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var controller = _fileUploader.UploadFromStream(
                contentStream,
                thumbnails,
                (progress, total) => _progressCallback?.Invoke(new Progress(progress, total)),
                cancellationToken);

            var (fileNodeUid, fileRevisionUid) = await controller.Completion.ConfigureAwait(false);

            // NOTE: Sha1Digest and SizeOnStorage are not available when using SDK
            FileInfo = FileInfo.Copy()
                .WithId(fileNodeUid.ToString().Split('~')[1])
                .WithRevisionId(fileRevisionUid.ToString().Split('~')[2]);
        }
        catch (Exception ex) when (ExceptionMapping.TryMapSdkClientException(ex, FileInfo.Id, includeObjectId: false, out var mappedException))
        {
            if (ExceptionMapping.IsSdkIntegrityFailure(ex))
            {
                _reportIntegrityFailure.Invoke(ex);
            }

            throw mappedException;
        }
    }

    public Task<NodeInfo<string>> FinishAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(FileInfo);
    }

    public ValueTask DisposeAsync()
    {
        _fileUploader.Dispose();

        return ValueTask.CompletedTask;
    }
}
