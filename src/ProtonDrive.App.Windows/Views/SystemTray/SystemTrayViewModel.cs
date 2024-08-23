using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using ProtonDrive.App.Windows.Views.Main.Activity;
using ProtonDrive.App.Windows.Views.Shared;

namespace ProtonDrive.App.Windows.Views.SystemTray;

internal sealed class SystemTrayViewModel : ObservableObject
{
    private readonly AppCommands _appCommands;
    private readonly AppStateViewModel _appStateViewModel;

    public SystemTrayViewModel(
        AppCommands appCommands,
        AppStateViewModel appStateViewModel,
        SyncStateViewModel syncStatusViewModel)
    {
        _appCommands = appCommands;
        _appStateViewModel = appStateViewModel;

        SyncStatusViewModel = syncStatusViewModel;

        _appStateViewModel.PropertyChanged += AppViewModelOnPropertyChanged;
        SyncStatusViewModel.PropertyChanged += SyncStatusViewModelOnPropertyChanged;
    }

    public SyncStateViewModel SyncStatusViewModel { get; }

    public ICommand SignInCommand => _appCommands.SignInCommand;
    public ICommand OpenAccountRootFolderCommand => _appCommands.OpenAccountRootFolderCommand;
    public ICommand OpenDriveOnlineCommand => _appCommands.OpenDriveOnlineCommand;
    public ICommand ShowMainWindowCommand => _appCommands.ActivateCommand;
    public ICommand ExitCommand => _appCommands.ExitCommand;

    public AppIconStatus AppIconStatus => _appStateViewModel.IconStatus;
    public AppDisplayStatus AppDisplayStatus => _appStateViewModel.DisplayStatus;

    public bool SynchronizationPaused
    {
        get => SyncStatusViewModel.Paused;
        set => SyncStatusViewModel.Paused = value;
    }

    private void AppViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppStateViewModel.IconStatus))
        {
            OnPropertyChanged(nameof(AppIconStatus));
        }

        if (e.PropertyName == nameof(AppStateViewModel.DisplayStatus))
        {
            OnPropertyChanged(nameof(AppDisplayStatus));
        }
    }

    private void SyncStatusViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SyncStatusViewModel.Paused))
        {
            OnPropertyChanged(nameof(SynchronizationPaused));
        }
    }
}
