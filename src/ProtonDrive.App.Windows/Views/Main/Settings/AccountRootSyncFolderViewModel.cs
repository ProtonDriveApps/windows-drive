using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.App.Windows.Toolkit.Threading;
using ProtonDrive.Shared.Configuration;

namespace ProtonDrive.App.Windows.Views.Main.Settings;

internal sealed class AccountRootSyncFolderViewModel : ObservableObject, ISessionStateAware, ISyncFoldersAware
{
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ILocalFolderService _localFolderService;
    private readonly ISyncFolderService _syncFolderService;
    private readonly AppConfig _appConfig;
    private readonly DispatcherScheduler _scheduler;

    private readonly AsyncRelayCommand _selectFolderCommand;

    private SessionState _sessionState = SessionState.None;
    private SyncFolder? _accountRootFolder;
    private MappingSetupStatus _status;
    private MappingErrorCode _errorCode;
    private SyncFolderValidationResult _validationResult;

    public AccountRootSyncFolderViewModel(
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ILocalFolderService localFolderService,
        ISyncFolderService syncFolderService,
        AppConfig appConfig,
        DispatcherScheduler scheduler)
    {
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _localFolderService = localFolderService;
        _syncFolderService = syncFolderService;
        _appConfig = appConfig;
        _scheduler = scheduler;

        _selectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
    }

    public ICommand SelectFolderCommand => _selectFolderCommand;

    public MappingSetupStatus Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public MappingErrorCode ErrorCode
    {
        get => _errorCode;
        set => SetProperty(ref _errorCode, value);
    }

    public string? LocalFolderPath => _accountRootFolder?.LocalPath;

    public ImageSource? FolderIcon => LocalFolderPath is not null
        ? _fileSystemDisplayNameAndIconProvider.GetFolderIconWithoutAccess(LocalFolderPath, ShellIconSize.Large)
        : default;

    public SyncFolderValidationResult ValidationResult
    {
        get => _validationResult;
        private set => SetProperty(ref _validationResult, value);
    }

    public void ClearValidationResult()
    {
        ValidationResult = SyncFolderValidationResult.Succeeded;
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        _sessionState = value;

        ResetLocalFolder();
    }

    void ISyncFoldersAware.OnSyncFolderChanged(SyncFolderChangeType changeType, SyncFolder folder)
    {
        if (folder.Type is not SyncFolderType.AccountRoot)
        {
            return;
        }

        if (changeType is SyncFolderChangeType.Removed)
        {
            _accountRootFolder = null;
            Status = MappingSetupStatus.None;
            ErrorCode = MappingErrorCode.None;
        }
        else
        {
            _accountRootFolder = folder;
            Status = folder.Status;
            ErrorCode = folder.ErrorCode;
        }

        ResetLocalFolder();
    }

    private void ResetLocalFolder()
    {
        ValidationResult = SyncFolderValidationResult.Succeeded;

        Schedule(UpdateCommands);
    }

    private async Task SelectFolderAsync()
    {
        var selectedPath = BrowseForFolder();

        if (string.IsNullOrEmpty(selectedPath))
        {
            return;
        }

        if (LocalFolderPath?.Equals(selectedPath, StringComparison.Ordinal) == true)
        {
            return;
        }

        if (_sessionState.Status is not SessionStatus.Started)
        {
            return;
        }

        if (ValidateAccountSyncFolder(selectedPath))
        {
            await _syncFolderService.SetAccountRootFolderAsync(selectedPath).ConfigureAwait(true);
        }
    }

    private string? BrowseForFolder()
    {
        var initialDirectoryPath = Path.GetDirectoryName(LocalFolderPath);
        if (string.IsNullOrEmpty(initialDirectoryPath) || !_localFolderService.FolderExists(initialDirectoryPath))
        {
            initialDirectoryPath = _appConfig.UserDataPath;
        }

        var folderPickingDialog = new OpenFolderDialog
        {
            InitialDirectory = initialDirectoryPath,
        };

        var result = folderPickingDialog.ShowDialog();

        return result is true ? folderPickingDialog.FolderName : null;
    }

    private bool ValidateAccountSyncFolder(string path)
    {
        ValidationResult = _syncFolderService.ValidateAccountRootFolder(path);

        return ValidationResult == SyncFolderValidationResult.Succeeded;
    }

    private void UpdateCommands()
    {
        OnPropertyChanged(nameof(LocalFolderPath));
        OnPropertyChanged(nameof(FolderIcon));

        _selectFolderCommand.NotifyCanExecuteChanged();
    }

    private void Schedule(Action origin)
    {
        _scheduler.Schedule(origin);
    }
}
