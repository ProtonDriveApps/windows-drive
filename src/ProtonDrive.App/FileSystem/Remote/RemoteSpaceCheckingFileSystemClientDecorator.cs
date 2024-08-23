using System;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Account;
using ProtonDrive.Client;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Remote;

internal sealed class RemoteSpaceCheckingFileSystemClientDecorator : FileSystemClientDecoratorBase<string>
{
    private readonly IUserService _userService;
    private readonly StorageReservationHandler _storageReservationHandler;

    public RemoteSpaceCheckingFileSystemClientDecorator(
        IUserService userService,
        StorageReservationHandler storageReservationHandler,
        IFileSystemClient<string> fileSystemClient)
        : base(fileSystemClient)
    {
        _userService = userService;
        _storageReservationHandler = storageReservationHandler;
    }

    public override async Task<IRevisionCreationProcess<string>> CreateFile(
        NodeInfo<string> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var storageReservation = await ReserveStorageOrThrowAsync(info, cancellationToken).ConfigureAwait(false);

        try
        {
            var creationProcess = await base.CreateFile(info, tempFileName, thumbnailProvider, progressCallback, cancellationToken).ConfigureAwait(false);
            return new StorageReservingRevisionCreationProcessDecorator(creationProcess, storageReservation, _userService);
        }
        catch
        {
            storageReservation.Dispose();
            throw;
        }
    }

    public override async Task<IRevisionCreationProcess<string>> CreateRevision(
        NodeInfo<string> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        var storageReservation = await ReserveStorageOrThrowAsync(info.Id, size, cancellationToken).ConfigureAwait(false);

        try
        {
            var creationProcess = await base.CreateRevision(info, size, lastWriteTime, tempFileName, thumbnailProvider, progressCallback, cancellationToken)
                .ConfigureAwait(false);
            return new StorageReservingRevisionCreationProcessDecorator(creationProcess, storageReservation, _userService);
        }
        catch
        {
            storageReservation.Dispose();
            throw;
        }
    }

    private Task<IDisposable> ReserveStorageOrThrowAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        return ReserveStorageOrThrowAsync(info.Id, info.Size, cancellationToken);
    }

    private async Task<IDisposable> ReserveStorageOrThrowAsync(string? fileId, long fileSize, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userService.GetUserAsync(cancellationToken).ConfigureAwait(false);

            var numberOfBlocksInFile = (fileSize + Constants.FileBlockSize - 1) / Constants.FileBlockSize;

            var estimatedEncryptedFileSize = fileSize + (numberOfBlocksInFile * Constants.MaxBlockEncryptionOverhead)
                                                      + (Constants.MaxThumbnailSize + Constants.MaxBlockEncryptionOverhead);

            var availableSpace = user.MaxSpace - user.UsedSpace;

            if (!_storageReservationHandler.TryReserve(estimatedEncryptedFileSize, availableSpace, out var reservation))
            {
                throw new FileSystemClientException<string>("Not enough free space to upload the file.", FileSystemErrorCode.FreeSpaceExceeded, fileId);
            }

            return reservation;
        }
        catch (ApiException apiException)
        {
            var errorCode = apiException.ResponseCode switch
            {
                ResponseCode.Timeout => FileSystemErrorCode.TimedOut,
                ResponseCode.Offline => FileSystemErrorCode.Offline,
                ResponseCode.RequestTimeout => FileSystemErrorCode.TimedOut,
                _ => FileSystemErrorCode.Unknown,
            };

            throw new FileSystemClientException<string>(apiException.Message, errorCode, null);
        }
    }
}
