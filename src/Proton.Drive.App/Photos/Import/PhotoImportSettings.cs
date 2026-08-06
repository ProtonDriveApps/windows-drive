namespace Proton.Drive.App.Photos.Import;

public sealed record PhotoImportSettings(IReadOnlyCollection<PhotoImportFolderState> Folders);
