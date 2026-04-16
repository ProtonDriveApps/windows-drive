using ProtonDrive.Sync.Shared.FileSystem.Photos;

namespace ProtonDrive.App.Photos.Import;

public sealed class PhotoImportFolderState
{
    public PhotoImportFolderState(int mappingId, string path)
    {
        MappingId = mappingId;
        Path = path;
    }

    public int MappingId { get; init; }

    public string Path { get; init; }

    public PhotoImportFolderStatus Status { get; set; }

    public int NumberOfFilesToImport { get; set; }

    public int NumberOfImportedFiles { get; set; }

    public PhotoImportFolderCurrentPosition? CurrentPosition { get; set; }

    public PhotoImportErrorCode? ErrorCode { get; set; }
}
