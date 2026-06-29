namespace Proton.Drive.App.Photos.Import;

internal readonly struct PhotoImportPipelineParameters
{
    public PhotoImportPipelineParameters(
        string volumeId,
        string shareId,
        string parentLinkId,
        string folderPath,
        PhotoImportFolderCurrentPosition? folderCurrentPosition,
        int maxNumberOfConcurrentFileTransfers,
        int duplicationCheckBatchSize)
    {
        VolumeId = volumeId;
        ShareId = shareId;
        ParentLinkId = parentLinkId;
        FolderPath = folderPath;
        FolderCurrentPosition = folderCurrentPosition;
        MaxNumberOfConcurrentFileTransfers = maxNumberOfConcurrentFileTransfers;
        DuplicationCheckBatchSize = duplicationCheckBatchSize;
    }

    public string VolumeId { get; }
    public string ShareId { get; }
    public string ParentLinkId { get; }
    public string FolderPath { get; }
    public PhotoImportFolderCurrentPosition? FolderCurrentPosition { get; }
    public int MaxNumberOfConcurrentFileTransfers { get; }
    public int DuplicationCheckBatchSize { get; }
}
