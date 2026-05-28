using CommunityToolkit.Mvvm.ComponentModel;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Photos.Import;
using ProtonDrive.Sync.Shared.FileSystem.Photos;

namespace ProtonDrive.App.Windows.Views.Main.Photos;

internal sealed class ImportFolderViewModel : ObservableObject
{
    private PhotoImportFolderStatus _importStatus;
    private int? _numberOfFilesToImport;
    private int _numberOfImportedFiles;
    private bool _noPhotosFound;
    private PhotoImportErrorCode? _importErrorCode;

    public ImportFolderViewModel(string name, PhotoImportFolderState folder)
    {
        Name = name;
        Path = folder.Path;
        ImportFolder = folder;
        Update();
    }

    public ImportFolderViewModel(string path, string name, SyncFolderValidationResult validationResult)
    {
        Name = name;
        Path = path;
        ValidationResult = validationResult;
        ImportStatus = PhotoImportFolderStatus.ValidationFailed;
    }

    public PhotoImportFolderState? ImportFolder { get; }

    public string Path { get; }
    public string Name { get; }
    public SyncFolderValidationResult ValidationResult { get; }

    public PhotoImportErrorCode? ImportErrorCode
    {
        get => _importErrorCode;
        private set => SetProperty(ref _importErrorCode, value);
    }

    public PhotoImportFolderStatus ImportStatus
    {
        get => _importStatus;
        set => SetProperty(ref _importStatus, value);
    }

    public int? NumberOfFilesToImport
    {
        get => _numberOfFilesToImport;
        set => SetProperty(ref _numberOfFilesToImport, value);
    }

    public int NumberOfImportedFiles
    {
        get => _numberOfImportedFiles;
        set => SetProperty(ref _numberOfImportedFiles, value);
    }

    public bool NoPhotosFound
    {
        get => _noPhotosFound;
        private set => SetProperty(ref _noPhotosFound, value);
    }

    public void Update()
    {
        ImportStatus = ImportFolder?.Status ?? PhotoImportFolderStatus.NotStarted;
        ImportErrorCode = ImportStatus is PhotoImportFolderStatus.Failed ? ImportFolder?.ErrorCode ?? PhotoImportErrorCode.Unknown : null;

        NumberOfFilesToImport = ImportFolder?.NumberOfFilesToImport;
        NumberOfImportedFiles = ImportFolder?.NumberOfImportedFiles ?? 0;

        NoPhotosFound = ImportStatus is PhotoImportFolderStatus.Succeeded && NumberOfFilesToImport == 0 && NumberOfImportedFiles == 0;
    }
}
