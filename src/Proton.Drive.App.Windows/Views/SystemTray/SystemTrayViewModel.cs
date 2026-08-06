using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Proton.Drive.App.Windows.Views.Main.Activity;
using Proton.Drive.App.Windows.Views.Shared;

namespace Proton.Drive.App.Windows.Views.SystemTray;

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

        _appStateViewModel.PropertyChanged += OnAppViewModelPropertyChanged;
        SyncStatusViewModel.PropertyChanged += OnSyncStatusViewModelPropertyChanged;
    }

    public SyncStateViewModel SyncStatusViewModel { get; }

    public ICommand SignInCommand => _appCommands.SignInCommand;
    public ICommand OpenAccountRootFolderCommand => _appCommands.OpenAccountRootFolderCommand;
    public ICommand OpenDriveOnlineCommand => _appCommands.OpenDriveOnlineCommand;
    public ICommand ShowAppCommand => _appCommands.ActivateCommand;
    public ICommand ExitCommand => _appCommands.ExitCommand;
    public ICommand PauseSyncTemporarilyCommand => SyncStatusViewModel.PauseSyncTemporarilyCommand;
    public ICommand PauseSyncUntilTomorrowMorningCommand => SyncStatusViewModel.PauseSyncUntilTomorrowMorningCommand;
    public ICommand ResumeSyncCommand => SyncStatusViewModel.ResumeSyncCommand;

    public AppIconStatus AppIconStatus => _appStateViewModel.IconStatus;
    public AppDisplayStatus AppDisplayStatus => _appStateViewModel.DisplayStatus;

    public bool SynchronizationPaused
    {
        get => SyncStatusViewModel.Paused;
        set => SyncStatusViewModel.Paused = value;
    }

    private void OnAppViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
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

    private void OnSyncStatusViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SyncStatusViewModel.Paused))
        {
            OnPropertyChanged(nameof(SynchronizationPaused));
        }
    }
}
