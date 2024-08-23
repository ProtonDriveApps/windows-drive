using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Account;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Remote;

internal class StorageReservingRevisionCreationProcessDecorator : IRevisionCreationProcess<string>
{
    private static readonly SemaphoreSlim UsedSpaceUpdateSemaphore = new(1, 1);

    private readonly IRevisionCreationProcess<string> _decoratedInstance;
    private readonly IDisposable _storageReservation;
    private readonly IUserService _userService;

    public StorageReservingRevisionCreationProcessDecorator(
        IRevisionCreationProcess<string> decoratedInstance,
        IDisposable storageReservation,
        IUserService userService)
    {
        _decoratedInstance = decoratedInstance;
        _storageReservation = storageReservation;
        _userService = userService;
    }

    public NodeInfo<string> FileInfo => _decoratedInstance.FileInfo;

    public NodeInfo<string> BackupInfo
    {
        get => _decoratedInstance.BackupInfo;
        set => _decoratedInstance.BackupInfo = value;
    }

    public bool ImmediateHydrationRequired => _decoratedInstance.ImmediateHydrationRequired;

    public Stream OpenContentStream()
    {
        return _decoratedInstance.OpenContentStream();
    }

    public async Task<NodeInfo<string>> FinishAsync(CancellationToken cancellationToken)
    {
        var fileInfo = await _decoratedInstance.FinishAsync(cancellationToken).ConfigureAwait(false);

        if (fileInfo.SizeOnStorage is not null)
        {
            await UsedSpaceUpdateSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var user = await _userService.GetUserAsync(cancellationToken).ConfigureAwait(false);

                _userService.ApplyUpdate(usedSpace: user.UsedSpace + fileInfo.SizeOnStorage.Value);
            }
            finally
            {
                UsedSpaceUpdateSemaphore.Release();
            }
        }

        return fileInfo;
    }

    public async ValueTask DisposeAsync()
    {
        _storageReservation.Dispose();
        await _decoratedInstance.DisposeAsync().ConfigureAwait(false);
    }
}
