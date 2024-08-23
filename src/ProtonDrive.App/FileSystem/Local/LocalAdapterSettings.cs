using System;

namespace ProtonDrive.App.FileSystem.Local;

internal sealed record LocalAdapterSettings
{
    private readonly string? _tempFolderName;
    private readonly string? _backupFolderName;
    private readonly string? _trashFolderName;
    private readonly string? _editConflictNamePattern;
    private readonly string? _deletedNamePattern;

    public string TempFolderName
    {
        get => _tempFolderName ?? throw new ArgumentNullException(nameof(TempFolderName));
        init => _tempFolderName = value;
    }

    /// <summary>
    /// A name of the folder on the replica root that contains local backup files.
    /// </summary>
    /// <remarks>
    /// The backup folder is not used anymore. Value still used for the backup folder
    /// to be excluded from syncing if it exists on the local replica.
    /// </remarks>
    public string BackupFolderName
    {
        get => _backupFolderName ?? throw new ArgumentNullException(nameof(BackupFolderName));
        init => _backupFolderName = value;
    }

    public string TrashFolderName
    {
        get => _trashFolderName ?? throw new ArgumentNullException(nameof(TrashFolderName));
        init => _trashFolderName = value;
    }

    public string EditConflictNamePattern
    {
        get => _editConflictNamePattern ?? throw new ArgumentNullException(nameof(EditConflictNamePattern));
        init => _editConflictNamePattern = value;
    }

    public string DeletedNamePattern
    {
        get => _deletedNamePattern ?? throw new ArgumentNullException(nameof(DeletedNamePattern));
        init => _deletedNamePattern = value;
    }
}
