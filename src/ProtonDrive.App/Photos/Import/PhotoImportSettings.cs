namespace ProtonDrive.App.Photos.Import;

public sealed record PhotoImportSettings(IReadOnlyCollection<PhotoImportFolderState> Folders);
