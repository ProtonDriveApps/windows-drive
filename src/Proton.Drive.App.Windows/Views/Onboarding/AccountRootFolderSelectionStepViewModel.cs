using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Proton.Drive.App.Onboarding;
using Proton.Drive.App.Windows.SystemIntegration;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Mapping.SyncFolders;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Shared.Configuration;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Windows.Views.Onboarding;

internal sealed class AccountRootFolderSelectionStepViewModel : OnboardingStepViewModel, ISessionStateAware
{
    private readonly AppConfig _appConfig;
    private readonly IOnboardingService _onboardingService;
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ISyncFolderService _syncFolderService;
    private readonly ILocalFolderService _localFolderService;
    private readonly CoalescingAction _setupDefaultLocalFolderPath;

    private SessionState _sessionState = SessionState.None;
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
        [FromKeyedServices("Dispatcher")] IScheduler scheduler)
    {
        _appConfig = appConfig;
        _onboardingService = onboardingService;
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _syncFolderService = syncFolderService;
        _localFolderService = localFolderService;

        ContinueCommand = new AsyncRelayCommand(ContinueAsync, CanContinue);
        ChangeSyncFolderCommand = new RelayCommand(ChangeSyncFolder, CanChangeSyncFolder);

        _setupDefaultLocalFolderPath = new CoalescingAction(scheduler, SetUpDefaultLocalFolderPath);
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

    public override void Activate()
    {
        base.Activate();

        _isSyncingCloudFilesAllowed = false;

        TrySetUpDefaultLocalFolderPath();
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        _sessionState = value;

        TrySetUpDefaultLocalFolderPath();
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

        var folderPickingDialog = new OpenFolderDialog
        {
            InitialDirectory = initialDirectoryPath,
        };

        var result = folderPickingDialog.ShowDialog();

        return result is true ? folderPickingDialog.FolderName : null;
    }

    private bool CanContinue()
    {
        return IsActive;
    }

    private async Task ContinueAsync()
    {
        ValidateLocalFolder();

        if (!_isSyncingCloudFilesAllowed)
        {
            await DelayBeforeSwitchingStepAsync().ConfigureAwait(true);

            return;
        }

        var path = LocalFolderPath;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        await DelayBeforeSwitchingStepAsync().ConfigureAwait(true);

        await _syncFolderService.SetAccountRootFolderAsync(path).ConfigureAwait(true);

        _onboardingService.CompleteStep(OnboardingStep.AccountRootFolderSelection);
    }

    private void TrySetUpDefaultLocalFolderPath()
    {
        if (_sessionState.Status is SessionStatus.Ending or SessionStatus.NotStarted or SessionStatus.SigningIn or SessionStatus.Starting)
        {
            LocalFolderPath = default;

            return;
        }

        if (IsActive && string.IsNullOrEmpty(LocalFolderPath))
        {
            _setupDefaultLocalFolderPath.Run();
        }
    }

    private void SetUpDefaultLocalFolderPath()
    {
        if (!string.IsNullOrEmpty(LocalFolderPath))
        {
            return;
        }

        var sessionState = _sessionState;

        var defaultFolderName = !string.IsNullOrEmpty(sessionState.Username)
            ? sessionState.Username
            : sessionState.UserEmailAddress?.Split("@").FirstOrDefault();

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
