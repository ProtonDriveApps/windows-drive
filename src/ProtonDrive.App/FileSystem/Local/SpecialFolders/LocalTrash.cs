using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Local.SpecialFolders;

internal sealed class LocalTrash<TId> : SpecialFolder<TId>, ILocalTrash<TId>, IDisposable
    where TId : IEquatable<TId>
{
    private readonly IFileSystemClient<TId> _fileSystemClient;
    private readonly IScheduler _scheduler;
    private readonly TimeSpan _emptyInterval;
    private readonly ILogger<LocalTrash<TId>> _logger;

    private readonly CoalescingAction _emptyTrash;

    private ISchedulerTimer? _emptyTrashTimer;

    public LocalTrash(
        string name,
        ISpecialFolder<TId> parentFolder,
        IFileSystemClient<TId> fileSystemClient,
        IScheduler scheduler,
        TimeSpan emptyInterval,
        ILogger<LocalTrash<TId>> logger)
        : base(name, parentFolder, fileSystemClient, logger)
    {
        _fileSystemClient = fileSystemClient;
        _scheduler = scheduler;
        _emptyInterval = emptyInterval;
        _logger = logger;

        _emptyTrash = _logger.GetCoalescingActionWithExceptionsLoggingAndCancellationHandling(EmptyTrashInternal, nameof(LocalTrash<TId>));
    }

    public void StartAutomaticDisposal()
    {
        _emptyTrashTimer = _scheduler.CreateTimer();
        _emptyTrashTimer.Interval = _emptyInterval;
        _emptyTrashTimer.Tick += (_, _) => Empty();
        _emptyTrashTimer.Start();
    }

    public Task StopAutomaticDisposalAsync()
    {
        _emptyTrashTimer?.Stop();
        _emptyTrashTimer?.Dispose();
        _emptyTrashTimer = null;

        _emptyTrash.Cancel();

        return WaitForCompletionAsync();
    }

    public Task Empty()
    {
        return _emptyTrash.Run();
    }

    public void Dispose()
    {
        _emptyTrashTimer?.Dispose();
        _emptyTrashTimer = null;
    }

    internal Task WaitForCompletionAsync()
    {
        // Wait for all scheduled tasks to complete
        return _emptyTrash.CurrentTask;
    }

    private async Task EmptyTrashInternal(CancellationToken cancellationToken)
    {
        try
        {
            var trashFolder = await GetOrCreate(cancellationToken).ConfigureAwait(false);

            await foreach (var child in _fileSystemClient.Enumerate(trashFolder, cancellationToken))
            {
                // Enumerate does not fill the Path
                var node = child.Copy().WithPath(Path.Combine(trashFolder.Path, child.Name));

                await TryDeleteAsync(node, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (FileSystemClientException ex)
        {
            _logger.LogWarning("Failed to empty local trash: {ErrorMessage}", ex.Message);
        }
    }

    private async Task TryDeleteAsync(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        var nameToLog = _logger.GetSensitiveValueForLogging(info.Name);

        try
        {
            await _fileSystemClient.DeletePermanently(info, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Deleted locally trashed {NodeType} \"{Name}\" with external Id={Id}", ToType(info.Attributes), nameToLog, info.Id);
        }
        catch (FileSystemClientException ex)
        {
            _logger.LogWarning("Failed to delete locally trashed {NodeType} \"{Name}\" with external Id={Id}: {ErrorMessage}", ToType(info.Attributes), nameToLog, info.Id, ex.Message);
        }
    }

    private string ToType(FileAttributes attributes)
    {
        return attributes.HasFlag(FileAttributes.Directory)
            ? "Folder"
            : "File";
    }
}
