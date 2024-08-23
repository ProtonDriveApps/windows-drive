using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Local.SpecialFolders;

internal class SpecialFolder<TId> : ISpecialFolder<TId>
    where TId : IEquatable<TId>
{
    private readonly string _name;
    private readonly ISpecialFolder<TId> _parentFolder;
    private readonly IFileSystemClient<TId> _fileSystemClient;
    private readonly ILogger<SpecialFolder<TId>> _logger;
    private readonly bool _hidden;

    private readonly SingleFunction<NodeInfo<TId>> _getFolder;

    public SpecialFolder(
        string name,
        ISpecialFolder<TId> parentFolder,
        IFileSystemClient<TId> fileSystemClient,
        ILogger<SpecialFolder<TId>> logger,
        bool hidden = false)
    {
        _fileSystemClient = fileSystemClient;
        _logger = logger;
        _hidden = hidden;
        _parentFolder = parentFolder;
        _name = Ensure.NotNullOrEmpty(name, nameof(name));

        _getFolder = new SingleFunction<NodeInfo<TId>>(GetOrCreateInternal!);
    }

    public Task<NodeInfo<TId>> GetOrCreate(CancellationToken cancellationToken)
    {
        return _getFolder.RunAsync(cancellationToken)!;
    }

    private async Task<NodeInfo<TId>> GetOrCreateInternal(CancellationToken cancellationToken)
    {
        var parentInfo = await _parentFolder.GetOrCreate(cancellationToken).ConfigureAwait(false);

        var folderInfo = NodeInfo<TId>.Directory()
            .WithParentId(parentInfo.Id)
            .WithPath(Path.Combine(parentInfo.Path, _name))
            .WithName(_name)
            .WithAttributes(FileAttributes.Directory | (_hidden ? FileAttributes.Hidden : default));

        try
        {
            var folder = await _fileSystemClient.GetInfo(folderInfo, cancellationToken).ConfigureAwait(false);

            // GetInfo does not fill the Path
            return folder.Copy().WithPath(folderInfo.Path);
        }
        catch (FileSystemClientException ex) when (ex.ErrorCode is FileSystemErrorCode.PathNotFound or FileSystemErrorCode.DirectoryNotFound)
        {
            return await CreateFolder(folderInfo, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<NodeInfo<TId>> CreateFolder(NodeInfo<TId> info, CancellationToken cancellationToken)
    {
        try
        {
            var folderInfo = await _fileSystemClient.CreateDirectory(info, cancellationToken).ConfigureAwait(false);

            // CreateDirectory does not fill the Path
            folderInfo = folderInfo.Copy().WithPath(info.Path);

            _logger.LogInformation("Created a special folder \"{Name}\"", info.Name);

            return folderInfo;
        }
        catch (FileSystemClientException ex)
        {
            throw new FileSystemClientException($"Failed to create a special folder \"{info.Name}\"", FileSystemErrorCode.Unknown, ex);
        }
    }
}
