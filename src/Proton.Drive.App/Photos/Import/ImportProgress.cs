namespace Proton.Drive.App.Photos.Import;

internal sealed class ImportProgress
{
    private readonly ImportProgressCallbacks _callbacks;

    private int _numberOfImportedFiles;
    private int _numberOfFilesToImport;

    public ImportProgress(ImportProgressCallbacks callbacks)
    {
        _callbacks = callbacks;
    }

    public void RaiseFileToImportFound()
    {
        _callbacks.OnProgressChanged?.Invoke(_numberOfImportedFiles, Interlocked.Increment(ref _numberOfFilesToImport));
    }

    public void RaiseFileImported()
    {
        _callbacks.OnProgressChanged?.Invoke(Interlocked.Increment(ref _numberOfImportedFiles), _numberOfFilesToImport);
    }

    public void RaiseFilesImported(int numberOfImportedFiles)
    {
        _callbacks.OnProgressChanged?.Invoke(Interlocked.Add(ref _numberOfImportedFiles, numberOfImportedFiles), _numberOfFilesToImport);
    }

    public void RaiseAlbumSelected(PhotoImportFolderCurrentPosition photoImportFolderCurrentPosition)
    {
        _callbacks.OnAlbumSelected?.Invoke(photoImportFolderCurrentPosition);
    }

    public void RaiseFileUploaded(string filePath)
    {
        _callbacks.OnPhotoFileActivityChanged?.Invoke(filePath, null);
    }

    public void RaiseFileUploadFailed(string filePath, Exception exception)
    {
        _callbacks.OnPhotoFileActivityChanged?.Invoke(filePath, exception);
    }
}
