using ProtonDrive.Client.Contracts;

namespace ProtonDrive.App.Settings;

public sealed class RemoteReplica
{
    public string? VolumeId { get; set; }
    public string? ShareId { get; set; }
    public string? RootLinkId { get; set; }
    public string? RootFolderName { get; set; }

    /// <summary>
    /// Automatically generated volume identity for internal use.
    /// </summary>
    public int InternalVolumeId { get; set; }

    public bool IsReadOnly { get; init; }
    public LinkType RootLinkType { get; set; } = LinkType.Folder;
}
