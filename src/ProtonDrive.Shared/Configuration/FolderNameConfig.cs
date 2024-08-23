namespace ProtonDrive.Shared.Configuration;

public class FolderNameConfig
{
    public string TempFolderName { get; set; } = string.Empty;

    /// <summary>
    /// A name of the folder on the replica root that contains local backup files.
    /// </summary>
    /// <remarks>
    /// The backup folder is not used anymore. Value still used for the backup folder
    /// to be excluded from syncing if it exists on the local replica.
    /// </remarks>
    public string BackupFolderName { get; set; } = string.Empty;

    public string TrashFolderName { get; set; } = string.Empty;
    public string CloudFilesFolderName { get; set; } = string.Empty;
    public string ForeignDevicesFolderName { get; set; } = string.Empty;
    public string SharedWithMeItemsFolderName { get; set; } = string.Empty;
}
