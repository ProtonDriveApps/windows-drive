using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.App.Windows.Configuration.Hyperlinks;
using ProtonDrive.App.Windows.Toolkit.Threading;

namespace ProtonDrive.App.Windows.Views.Shared;

internal class AppCommands : ISessionStateAware, ISyncFoldersAware
{
    private readonly IApp _app;
    private readonly IExternalHyperlinks _hyperlinks;
    private readonly ILocalFolderService _localFolderService;
    private readonly DispatcherScheduler _scheduler;
    private readonly AsyncRelayCommand _activateCommand;
    private readonly AsyncRelayCommand _signInCommand;
    private readonly AsyncRelayCommand _openAccountRootFolderCommand;

    private string? _accountRootFolder;
    private SessionStatus? _sessionStatus;

    public AppCommands(
        IApp app,
        IExternalHyperlinks hyperlinks,
        ILocalFolderService localFolderService,
        DispatcherScheduler scheduler)
    {
        _app = app;
        _hyperlinks = hyperlinks;
        _localFolderService = localFolderService;
        _scheduler = scheduler;

        _signInCommand = new AsyncRelayCommand(ActivateAsync, CanSignIn);
        _activateCommand = new AsyncRelayCommand(ActivateAsync, CanActivate);
        _openAccountRootFolderCommand = new AsyncRelayCommand(OpenAccountRootFolderAsync, CanOpenAccountRootFolder);
        OpenDriveOnlineCommand = new RelayCommand(OpenDriveOnline);
        ExitCommand = new AsyncRelayCommand(app.ExitAsync);
    }

    public ICommand SignInCommand => _signInCommand;
    public ICommand ActivateCommand => _activateCommand;
    public ICommand OpenAccountRootFolderCommand => _openAccountRootFolderCommand;
    public ICommand OpenDriveOnlineCommand { get; }
    public ICommand ExitCommand { get; }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        _sessionStatus = value.Status;
        Schedule(() =>
            {
                _signInCommand.NotifyCanExecuteChanged();
                _activateCommand.NotifyCanExecuteChanged();
                _openAccountRootFolderCommand.NotifyCanExecuteChanged();
            });
    }

    void ISyncFoldersAware.OnSyncFolderChanged(SyncFolderChangeType changeType, SyncFolder folder)
    {
        if (folder.Type is not SyncFolderType.AccountRoot)
        {
            return;
        }

        var path = changeType is not SyncFolderChangeType.Removed ? folder.LocalPath : null;
        if (path == _accountRootFolder && folder.Status != MappingSetupStatus.Succeeded)
        {
            return;
        }

        _accountRootFolder = path;
        Schedule(() => _openAccountRootFolderCommand.NotifyCanExecuteChanged());
    }

    private Task ActivateAsync()
    {
        return _app.ActivateAsync();
    }

    private bool CanSignIn()
    {
        return !IsUserSignedIn();
    }

    private bool CanActivate()
    {
        return IsUserSignedIn();
    }

    private void OpenDriveOnline()
    {
        _hyperlinks.WebClient.Open();
    }

    private async Task OpenAccountRootFolderAsync()
    {
        await _localFolderService.OpenFolderAsync(_accountRootFolder).ConfigureAwait(false);
    }

    private bool CanOpenAccountRootFolder()
    {
        return !_openAccountRootFolderCommand.IsRunning &&
               IsUserSignedIn() &&
               _localFolderService.FolderExists(_accountRootFolder);
    }

    private bool IsUserSignedIn()
    {
        return _sessionStatus is not SessionStatus.NotStarted and not SessionStatus.SigningIn and not SessionStatus.Ending;
    }

    private void Schedule(Action origin)
    {
        _scheduler.Schedule(origin);
    }
}
