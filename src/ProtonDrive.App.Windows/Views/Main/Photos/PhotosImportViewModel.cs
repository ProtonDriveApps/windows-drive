using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using ProtonDrive.App.Account;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.Photos;
using ProtonDrive.App.Photos.Import;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.App.Windows.Configuration.Hyperlinks;
using ProtonDrive.App.Windows.Extensions;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Windows.Views.Main.Photos;

internal sealed class PhotosImportViewModel : ObservableObject, IAccountSwitchingAware, IPhotoImportFoldersAware, IPhotosFeatureStateAware
{
    private readonly IPhotoImportFolderService _photoImportFolderService;
    private readonly IExternalHyperlinks _externalHyperlinks;
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ILocalFolderService _localFolderService;
    private readonly IScheduler _scheduler;

    private readonly AsyncRelayCommand _addFolderCommand;
    private readonly RelayCommand _displayImportGooglePhotosDetailsCommand;
    private readonly AsyncRelayCommand<ImportFolderViewModel?> _retryCommand;

    private string? _lastSelectedParentFolderPath;
    private bool _isEnabled;

    public PhotosImportViewModel(
        IPhotoImportFolderService photoImportFolderService,
        IExternalHyperlinks externalHyperlinks,
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ILocalFolderService localFolderService,
        [FromKeyedServices("Dispatcher")] IScheduler scheduler)
    {
        _photoImportFolderService = photoImportFolderService;
        _externalHyperlinks = externalHyperlinks;
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _localFolderService = localFolderService;
        _scheduler = scheduler;

        OpenHowToImportPhotosFromGoogleUrlCommand = new RelayCommand(OpenHowToImportPhotosFromGoogleUrl);
        OpenHowPhotoImportWorksUrlCommand = new RelayCommand(OpenHowImportWorksUrl);
        _displayImportGooglePhotosDetailsCommand = new RelayCommand(DisplayImportGooglePhotosDetails, CanAddFolder);
        _addFolderCommand = new AsyncRelayCommand(AddFolderAsync, CanAddFolder);
        OpenFolderCommand = new AsyncRelayCommand<ImportFolderViewModel?>(OpenFolderAsync);
        ManageAlbumsCommand = new RelayCommand<ImportFolderViewModel?>(ManageAlbums);
        _retryCommand = new AsyncRelayCommand<ImportFolderViewModel?>(RetryAsync, CanRetry);
        RemoveFolderCommand = new AsyncRelayCommand<ImportFolderViewModel?>(RemoveFolderAsync);
    }

    public bool IsDisplayingImportGooglePhotosDetails
    {
        get;
        set => SetProperty(ref field, value);
    }

    public ICommand OpenHowToImportPhotosFromGoogleUrlCommand { get; }

    public ICommand OpenHowPhotoImportWorksUrlCommand { get; }

    public ICommand DisplayImportGooglePhotosDetailsCommand => _displayImportGooglePhotosDetailsCommand;

    public ICommand AddFolderCommand => _addFolderCommand;

    public ICommand OpenFolderCommand { get; }

    public ICommand RetryCommand => _retryCommand;

    public ICommand ManageAlbumsCommand { get; }

    public ICommand RemoveFolderCommand { get; }

    public ObservableCollection<ImportFolderViewModel> Folders { get; } = [];

    public bool UploadingIsNotAvailable
    {
        get;
        private set => SetProperty(ref field, value);
    }

    void IPhotoImportFoldersAware.OnPhotoImportFolderChanged(SyncFolderChangeType changeType, PhotoImportFolderState folder)
    {
        Schedule(HandlePhotoImportFolderChange);

        return;

        void HandlePhotoImportFolderChange()
        {
            switch (changeType)
            {
                case SyncFolderChangeType.Added:
                    var folderName = _fileSystemDisplayNameAndIconProvider.GetDisplayNameWithoutAccess(folder.Path) ?? string.Empty;
                    var folderViewModel = new ImportFolderViewModel(folderName, folder);
                    Folders.Insert(0, folderViewModel);
                    break;

                case SyncFolderChangeType.Updated:
                    Folders.FirstOrDefault(x => x.ImportFolder == folder)?.Update();
                    _retryCommand.NotifyCanExecuteChanged();
                    break;

                case SyncFolderChangeType.Removed:
                    Folders.RemoveFirst(x => x.ImportFolder == folder);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(changeType), changeType, null);
            }
        }
    }

    void IAccountSwitchingAware.OnAccountSwitched()
    {
        Schedule(HandleAccountSwitched);
    }

    void IPhotosFeatureStateAware.OnPhotosFeatureStateChanged(PhotosFeatureState value)
    {
        UploadingIsNotAvailable = value.Status is PhotosFeatureStatus.ReadOnly or PhotosFeatureStatus.Disabled or PhotosFeatureStatus.Hidden;
        _isEnabled = value.Status is PhotosFeatureStatus.Ready;

        Schedule(RefreshCommands);
    }

    private static bool CanRetry(ImportFolderViewModel? folder)
    {
        return folder?.ImportStatus is PhotoImportFolderStatus.Failed;
    }

    private void OpenHowToImportPhotosFromGoogleUrl()
    {
        _externalHyperlinks.HowToImportPhotosFromGoogle.Open();
    }

    private void OpenHowImportWorksUrl()
    {
        _externalHyperlinks.HowPhotoImportWorks.Open();
    }

    private bool CanAddFolder()
    {
        return _isEnabled;
    }

    private async Task AddFolderAsync(CancellationToken cancellationToken)
    {
        var folderPickingDialog = new OpenFolderDialog
        {
            InitialDirectory = _lastSelectedParentFolderPath ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };

        var result = folderPickingDialog.ShowDialog();

        if (result is not true)
        {
            return;
        }

        var folderPath = folderPickingDialog.FolderName;
        _lastSelectedParentFolderPath = Path.GetDirectoryName(folderPath);

        if (Folders.Any(x => x.Path.Equals(folderPath)))
        {
            return;
        }

        var validationResult = _photoImportFolderService.ValidateFolder(folderPath);

        if (validationResult is SyncFolderValidationResult.Succeeded)
        {
            await _photoImportFolderService.AddFolderAsync(folderPath, cancellationToken).ConfigureAwait(true);
        }
        else
        {
            var folderName = _fileSystemDisplayNameAndIconProvider.GetDisplayNameWithoutAccess(folderPath) ?? string.Empty;
            var importFolder = new ImportFolderViewModel(folderPath, folderName, validationResult);

            Folders.Insert(0, importFolder);
        }

        IsDisplayingImportGooglePhotosDetails = false;
    }

    private async Task OpenFolderAsync(ImportFolderViewModel? folder, CancellationToken cancellationToken)
    {
        if (folder is null)
        {
            return;
        }

        await _localFolderService.OpenFolderAsync(folder.Path).ConfigureAwait(true);
    }

    private void ManageAlbums(ImportFolderViewModel? folder)
    {
        _externalHyperlinks.ManageAlbums.Open();
    }

    private async Task RetryAsync(ImportFolderViewModel? folder, CancellationToken cancellationToken)
    {
        if (folder?.ImportFolder is null)
        {
            return;
        }

        await _photoImportFolderService.RetryImportAsync(folder.ImportFolder, cancellationToken).ConfigureAwait(true);
    }

    private async Task RemoveFolderAsync(ImportFolderViewModel? folder, CancellationToken cancellationToken)
    {
        if (folder is null)
        {
            return;
        }

        if (folder.ImportFolder is not null)
        {
            await _photoImportFolderService.RemoveFolderAsync(folder.ImportFolder, cancellationToken).ConfigureAwait(true);
        }
        else
        {
            Folders.Remove(folder);
        }
    }

    private void DisplayImportGooglePhotosDetails()
    {
        IsDisplayingImportGooglePhotosDetails = true;
    }

    private void HandleAccountSwitched()
    {
        foreach (var folder in Folders.Where(x => x.ImportFolder is null).ToList())
        {
            Folders.Remove(folder);
        }

        IsDisplayingImportGooglePhotosDetails = false;
    }

    private void RefreshCommands()
    {
        _addFolderCommand.NotifyCanExecuteChanged();
        _displayImportGooglePhotosDetailsCommand.NotifyCanExecuteChanged();
    }

    private void Schedule(Action action)
    {
        _scheduler.Schedule(action);
    }
}
