namespace ProtonDrive.Sync.Shared.FileSystem;

public readonly record struct FileSystemClientParameters(
    string VolumeId,
    string ShareId,
    string? VirtualParentId = default,
    string? LinkId = default,
    string? LinkName = default);
