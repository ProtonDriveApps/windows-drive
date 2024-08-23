using System;
using Microsoft.Extensions.Logging;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Local.SpecialFolders;

internal sealed class HiddenSpecialFolder<TId> : SpecialFolder<TId>
    where TId : IEquatable<TId>
{
    public HiddenSpecialFolder(
        string name,
        ISpecialFolder<TId> parentFolder,
        IFileSystemClient<TId> fileSystemClient,
        ILogger<SpecialFolder<TId>> logger)
        : base(name, parentFolder, fileSystemClient, logger, hidden: true)
    {
    }
}
