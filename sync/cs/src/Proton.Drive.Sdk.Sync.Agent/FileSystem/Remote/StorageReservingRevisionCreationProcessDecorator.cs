using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Remote;

internal class StorageReservingRevisionCreationProcessDecorator : IDestinationRevision<string>
{
    private static readonly SemaphoreSlim UsedSpaceUpdateSemaphore = new(1, 1);

    private readonly IDestinationRevision<string> _decoratedInstance;
    private readonly IDisposable _storageReservation;
    private readonly IUserService _userService;

    public StorageReservingRevisionCreationProcessDecorator(
        IDestinationRevision<string> decoratedInstance,
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
    public bool ChecksumVerificationEnabled => _decoratedInstance.ChecksumVerificationEnabled;
    public bool CanGetContentStream => _decoratedInstance.CanGetContentStream;

    public Stream GetContentStream()
    {
        return _decoratedInstance.GetContentStream();
    }

    public Task WriteContentAsync(Stream source, FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
    {
        return _decoratedInstance.WriteContentAsync(source, expectedChecksum, cancellationToken);
    }

    public async Task<NodeInfo<string>> FinishAsync(FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
    {
        var fileInfo = await _decoratedInstance.FinishAsync(expectedChecksum, cancellationToken).ConfigureAwait(false);

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
