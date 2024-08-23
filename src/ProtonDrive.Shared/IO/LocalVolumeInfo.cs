namespace ProtonDrive.Shared.IO;

public sealed record LocalVolumeInfo
{
    public string VolumeName { get; init; } = string.Empty;
    public string FileSystemName { get; init; } = string.Empty;
    public int VolumeSerialNumber { get; init; }
    public int MaximumComponentLength { get; init; }
    public FileSystemAttributes Attributes { get; init; }
}
