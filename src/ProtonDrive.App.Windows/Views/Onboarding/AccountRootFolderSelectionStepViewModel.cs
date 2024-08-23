using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.Onboarding;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.App.Windows.Toolkit.Threading;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Windows.Views.Onboarding;

internal sealed class AccountRootFolderSelectionStepViewModel : OnboardingStepViewModel
{
    private readonly AppConfig _appConfig;
    private readonly IOnboardingService _onboardingService;
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ISyncFolderService _syncFolderService;
    private readonly ILocalFolderService _localFolderService;
    private readonly CoalescingAction _setupDefaultLocalFolderPath;

    private SyncFolderValidationResult _validationResult;
    private string? _localFolderPath;
    private ImageSource? _folderIcon;

    private bool _isSyncingCloudFilesAllowed;

    public AccountRootFolderSelectionStepViewModel(
        AppConfig appConfig,
        IOnboardingService onboardingService,
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ISyncFolderService syncFolderService,
        ILocalFolderService localFolderService,
        DispatcherScheduler scheduler)
    : base(scheduler)
    {
        _appConfig = appConfig;
        _onboardingService = onboardingService;
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _syncFolderService = syncFolderService;
        _localFolderService = localFolderService;

        ContinueCommand = new AsyncRelayCommand(ContinueAsync, CanContinue);
        ContinueCommand.PropertyChanged += OnAsyncRelayCommandPropertyChanged;
        ChangeSyncFolderCommand = new RelayCommand(ChangeSyncFolder, CanChangeSyncFolder);

        _setupDefaultLocalFolderPath = new CoalescingAction(scheduler, SetupDefaultLocalFolderPath);
    }

    public IAsyncRelayCommand ContinueCommand { get; }

    public IRelayCommand ChangeSyncFolderCommand { get; }

    public SyncFolderValidationResult ValidationResult
    {
        get => _validationResult;
        private set => SetProperty(ref _validationResult, value);
    }

    public string? LocalFolderPath
    {
        get => _localFolderPath;
        private set
        {
            if (SetProperty(ref _localFolderPath, value))
            {
                if (_localFolderPath is null)
                {
                    FolderIcon = default;
                    ValidationResult = default;

                    return;
                }

                FolderIcon = _fileSystemDisplayNameAndIconProvider.GetFolderIconWithoutAccess(_localFolderPath, ShellIconSize.Large);

                ValidateLocalFolder();
            }
        }
    }

    public ImageSource? FolderIcon
    {
        get => _folderIcon;
        private set => SetProperty(ref _folderIcon, value);
    }

    private void OnAsyncRelayCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is AsyncRelayCommand command && e.PropertyName == nameof(AsyncRelayCommand.IsRunning))
        {
            command.NotifyCanExecuteChanged();
            ChangeSyncFolderCommand.NotifyCanExecuteChanged();
        }
    }

    protected override void OnSessionStateChanged()
    {
        TrySetUpDefaultLocalFolderPath();
    }

    protected override void Activate()
    {
        IsActive = true;

        _isSyncingCloudFilesAllowed = false;

        TrySetUpDefaultLocalFolderPath();
    }

    protected override bool SkipActivation()
    {
        return _onboardingService.IsAccountRootFolderSelectionCompleted();
    }

    private bool CanChangeSyncFolder()
    {
        return !ContinueCommand.IsRunning;
    }

    private void ChangeSyncFolder()
    {
        var selectedPath = BrowseForFolder();

        if (string.IsNullOrEmpty(selectedPath))
        {
            return;
        }

        LocalFolderPath = selectedPath;
    }

    private string? BrowseForFolder()
    {
        var initialDirectoryPath = Path.GetDirectoryName(LocalFolderPath);
        if (string.IsNullOrEmpty(initialDirectoryPath) || !_localFolderService.FolderExists(initialDirectoryPath))
        {
            initialDirectoryPath = _appConfig.UserDataPath;
        }

        var folderPickingDialog = new OpenFolderDialog();
        folderPickingDialog.InitialDirectory = initialDirectoryPath;

        var result = folderPickingDialog.ShowDialog();

        return result is true ? folderPickingDialog.FolderName : null;
    }

    private bool CanContinue()
    {
        return IsActive && !ContinueCommand.IsRunning;
    }

    private async Task ContinueAsync()
    {
        ValidateLocalFolder();

        if (!_isSyncingCloudFilesAllowed)
        {
            // Display the progress spinner for a bit longer, so that the user can notice it
            await Task.Delay(DelayBeforeSwitchingStep).ConfigureAwait(true);

            return;
        }

        var path = LocalFolderPath;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        await _syncFolderService.SetAccountRootFolderAsync(path).ConfigureAwait(true);

        _onboardingService.SetAccountRootFolderSelectionCompleted();

        // Display the progress spinner for a bit longer, so that the user can notice it
        await Task.Delay(DelayBeforeSwitchingStep).ConfigureAwait(true);

        IsActive = false;
    }

    private void TrySetUpDefaultLocalFolderPath()
    {
        if (SessionState.Status is SessionStatus.Ending or SessionStatus.NotStarted or SessionStatus.SigningIn or SessionStatus.Starting)
        {
            LocalFolderPath = default;

            return;
        }

        if (IsActive && string.IsNullOrEmpty(LocalFolderPath))
        {
            _setupDefaultLocalFolderPath.Run();
        }
    }

    private void SetupDefaultLocalFolderPath()
    {
        if (!string.IsNullOrEmpty(LocalFolderPath))
        {
            return;
        }

        var defaultFolderName = !string.IsNullOrEmpty(SessionState.Username)
            ? SessionState.Username
            : SessionState.UserEmailAddress?.Split("@").FirstOrDefault();

        LocalFolderPath = _localFolderService.GetDefaultAccountRootFolderPath(_appConfig.UserDataPath, defaultFolderName);
    }

    private void ValidateLocalFolder()
    {
        if (string.IsNullOrEmpty(LocalFolderPath))
        {
            _isSyncingCloudFilesAllowed = false;
            return;
        }

        if (Directory.Exists(LocalFolderPath))
        {
            // Validate only if it is a user selection
            ValidationResult = _syncFolderService.ValidateAccountRootFolder(LocalFolderPath);
        }

        _isSyncingCloudFilesAllowed = ValidationResult is SyncFolderValidationResult.Succeeded;

        UpdateCommands();
    }

    private void UpdateCommands()
    {
        ContinueCommand.NotifyCanExecuteChanged();
    }
}
