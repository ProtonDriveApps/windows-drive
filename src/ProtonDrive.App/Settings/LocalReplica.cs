namespace ProtonDrive.App.Settings;

public class LocalReplica
{
    public int VolumeSerialNumber { get; set; }
    public string RootFolderPath { get; set; } = string.Empty;
    public long RootFolderId { get; set; }

    /// <summary>
    /// Automatically generated volume identity for internal use
    /// </summary>
    public int InternalVolumeId { get; set; }
}
