using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;

namespace Proton.Drive.App.Photos.Import;

public sealed class PhotoImportFolderState
{
    public PhotoImportFolderState(int id, string path)
    {
        Id = id;
        Path = path;
    }

    public int Id { get; init; }

    /// <summary>
    /// For deserialization compatibility with older versions, where MappingId contained identity value
    /// </summary>
    public int MappingId { init { Id = value; } }

    public string Path { get; init; }

    public PhotoImportFolderStatus Status { get; set; }

    public int NumberOfFilesToImport { get; set; }

    public int NumberOfImportedFiles { get; set; }

    public PhotoImportFolderCurrentPosition? CurrentPosition { get; set; }

    public PhotoImportErrorCode? ErrorCode { get; set; }
}
