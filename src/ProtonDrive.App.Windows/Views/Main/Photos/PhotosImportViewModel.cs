using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

internal sealed class PhotosImportViewModel : ObservableObject, ISyncFoldersAware, IAccountSwitchingAware, IPhotoImportFoldersAware, IPhotosFeatureStateAware
{
    private readonly IPhotoFolderService _photoFolderService;
    private readonly IExternalHyperlinks _externalHyperlinks;
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ILocalFolderService _localFolderService;
    private readonly IScheduler _scheduler;
    private readonly ILogger<PhotosImportViewModel> _logger;

    private readonly HashSet<PhotoImportFolderState> _photoImportFolders = [];
    private readonly AsyncRelayCommand _addFolderCommand;
    private readonly RelayCommand _displayImportGooglePhotosDetailsCommand;
    private readonly AsyncRelayCommand<ImportFolderViewModel?> _retryCommand;

    private bool _isDisplayingImportGooglePhotosDetails;
    private string? _lastSelectedParentFolderPath;
    private bool _uploadingIsNotAvailable;
    private bool _isEnabled;

    public PhotosImportViewModel(
        IPhotoFolderService photoFolderService,
        IExternalHyperlinks externalHyperlinks,
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ILocalFolderService localFolderService,
        [FromKeyedServices("Dispatcher")] IScheduler scheduler,
        ILogger<PhotosImportViewModel> logger)
    {
        _photoFolderService = photoFolderService;
        _externalHyperlinks = externalHyperlinks;
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _localFolderService = localFolderService;
        _scheduler = scheduler;
        _logger = logger;

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
        get => _isDisplayingImportGooglePhotosDetails;
        set => SetProperty(ref _isDisplayingImportGooglePhotosDetails, value);
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
        get => _uploadingIsNotAvailable;
        private set => SetProperty(ref _uploadingIsNotAvailable, value);
    }

    void ISyncFoldersAware.OnSyncFolderChanged(SyncFolderChangeType changeType, SyncFolder folder)
    {
        if (folder.Type is not SyncFolderType.PhotoImport)
        {
            return;
        }

        Schedule(HandleSyncFolderChange);

        return;

        void HandleSyncFolderChange()
        {
            switch (changeType)
            {
                case SyncFolderChangeType.Added:
                    var folderName = _fileSystemDisplayNameAndIconProvider.GetDisplayNameWithoutAccess(folder.LocalPath) ?? string.Empty;
                    var folderViewModel = new ImportFolderViewModel(folderName, folder);
                    var photoImportFolder = _photoImportFolders.FirstOrDefault(x => x.MappingId == folder.MappingId);
                    if (photoImportFolder is not null)
                    {
                        folderViewModel.Update(photoImportFolder);
                    }

                    Folders.Insert(0, folderViewModel);
                    break;

                case SyncFolderChangeType.Updated:
                    var item = Folders.FirstOrDefault(x => x.SyncFolder?.Equals(folder) ?? false);
                    item?.Update();
                    break;

                case SyncFolderChangeType.Removed:
                    Folders.RemoveFirst(x => x.SyncFolder?.Equals(folder) ?? false);
                    break;

                default:
                    throw new InvalidEnumArgumentException(nameof(changeType), (int)changeType, typeof(SyncFolderChangeType));
            }
        }
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
                    _photoImportFolders.Add(folder);
                    Folders.FirstOrDefault(x => x.SyncFolder?.MappingId == folder.MappingId)?.Update(folder);
                    break;

                case SyncFolderChangeType.Updated:
                    Folders.FirstOrDefault(x => x.SyncFolder?.MappingId == folder.MappingId)?.Update(folder);
                    _retryCommand.NotifyCanExecuteChanged();
                    break;

                case SyncFolderChangeType.Removed:
                    _photoImportFolders.Remove(folder);
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
        if (folder?.SyncFolder?.MappingId is null)
        {
            return false;
        }

        return folder.ImportStatus is PhotoImportFolderStatus.Failed;
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

        var validationResult = _photoFolderService.ValidateFolder(folderPath);

        if (validationResult is SyncFolderValidationResult.Succeeded)
        {
            await _photoFolderService.AddImportFolderAsync(folderPath, cancellationToken).ConfigureAwait(true);
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
        if (folder?.SyncFolder?.MappingId is null)
        {
            return;
        }

        await _photoFolderService.ResetImportFolderStatusAsync(folder.SyncFolder.MappingId, cancellationToken).ConfigureAwait(true);

        _logger.LogInformation("Requested retry to import folder with mapping \"{ID}\"", folder.SyncFolder.MappingId);
    }

    private async Task RemoveFolderAsync(ImportFolderViewModel? folder, CancellationToken cancellationToken)
    {
        if (folder is null)
        {
            return;
        }

        if (folder.SyncFolder is not null)
        {
            await _photoFolderService.RemoveFolderAsync(folder.SyncFolder, cancellationToken).ConfigureAwait(true);
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
        foreach (var folder in Folders.Where(x => x.SyncFolder is null).ToList())
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
