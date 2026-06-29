namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public readonly record struct FileSystemClientParameters(
    string VolumeId,
    string ShareId,
    string? VirtualParentId = null,
    string? LinkId = null,
    string? LinkName = null,
    bool IsPhotoClient = false);
