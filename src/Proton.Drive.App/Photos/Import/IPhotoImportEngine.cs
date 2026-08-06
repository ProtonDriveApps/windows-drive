namespace Proton.Drive.App.Photos.Import;

internal interface IPhotoImportEngine
{
    Task ImportAsync(ImportProgressCallbacks callbacks, CancellationToken cancellationToken);
}
