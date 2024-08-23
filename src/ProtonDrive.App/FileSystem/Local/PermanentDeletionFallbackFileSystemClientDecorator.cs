using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.FileSystem.Local.SpecialFolders;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Sync.Shared;
using ProtonDrive.Sync.Shared.FileSystem;
using ProtonDrive.Sync.Shared.Trees.FileSystem;

namespace ProtonDrive.App.FileSystem.Local;

internal sealed class PermanentDeletionFallbackFileSystemClientDecorator<TId> : FileSystemClientDecoratorBase<TId>
    where TId : IEquatable<TId>
{
    private readonly ILogger<PermanentDeletionFallbackFileSystemClientDecorator<TId>> _logger;
    private readonly ILocalTrash<TId> _trash;
    private readonly IFileNameFactory<TId> _trashedNameFactory;

    private NodeInfo<TId>? _trashDirectory;

    public PermanentDeletionFallbackFileSystemClientDecorator(
        ILogger<PermanentDeletionFallbackFileSystemClientDecorator<TId>> logger,
        ILocalTrash<TId> trash,
        IFileNameFactory<TId> trashedNameFactory,
        IFileSystemClient<TId> origin)
        : base(origin)
    {
        _trashedNameFactory = trashedNameFactory;
        _logger = logger;
        _trash = trash;
    }

    public override void Connect(string syncRootPath, IFileHydrationDemandHandler<TId> fileHydrationDemandHandler)
    {
        base.Connect(syncRootPath, fileHydrationDemandHandler);

        _trash.StartAutomaticDisposal();
    }

    public override async Task DisconnectAsync()
    {
        await _trash.StopAutomaticDisposalAsync().ConfigureAwait(false);

        await base.DisconnectAsync().ConfigureAwait(false);
    }

    public override async Task Delete(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        try
        {
            await base.Delete(info, cancellationToken).ConfigureAwait(false);
        }
        catch (FileSystemClientException ex)
        {
            _logger.LogWarning("Failed to move item to Recycle Bin, falling back to permanent deletion: {ErrorMessage}", ex.CombinedMessage());

            await FallBackToDeletePermanently().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to move item to Recycle Bin, falling back to permanent deletion");

            await FallBackToDeletePermanently().ConfigureAwait(false);
        }

        Task FallBackToDeletePermanently()
        {
            return info.IsDirectory()
                ? DeleteDirectoryPermanently(info, cancellationToken)
                : DeletePermanently(info, cancellationToken);
        }
    }

    private async Task DeleteDirectoryPermanently(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var trashInfo = await GetTrashDirectory(cancellationToken).ConfigureAwait(false);

        var trashedName = _trashedNameFactory.GetName(
            new FileSystemNodeModel<TId> { Type = NodeType.Directory, Id = info.Id!, Name = info.Name });

        var destinationInfo = info.Copy()
            .WithParentId(trashInfo.Id)
            .WithPath(Path.Combine(trashInfo.Path, trashedName))
            .WithName(trashedName);

        try
        {
            // Move to the trash folder instead of deleting in-place
            await Move(info, destinationInfo, cancellationToken).ConfigureAwait(false);

            var pathToLog = _logger.GetSensitiveValueForLogging(info.Path);
            _logger.LogInformation("Moved \"{Path}\" folder with external Id={Id} to the trash", pathToLog, info.Id);

            EmptyTrash();
        }
        catch (FileSystemClientException<TId> ex) when (!IsDefault(trashInfo.Id) && !IsDefault(ex.ObjectId) && ex.ObjectId!.Equals(trashInfo.Id))
        {
            // Clearing cached value ensures next time cache directory will be requested and created if necessary.
            _trashDirectory = null;

            throw new FileSystemClientException("Failed to access local trash directory", ex);
        }
    }

    private async Task<NodeInfo<TId>> GetTrashDirectory(CancellationToken cancellationToken)
    {
        return _trashDirectory ??= await _trash.GetOrCreate(cancellationToken).ConfigureAwait(false);
    }

    private void EmptyTrash()
    {
        // Trigger background task to permanently delete the content of the trash directory
        _ = _trash.Empty();
    }

    private bool IsDefault(TId? value) => value is null || value.Equals(default);
}
