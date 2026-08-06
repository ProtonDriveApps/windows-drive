using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Proton.Drive.App.Windows.Extensions;
using Proton.Drive.App.Windows.Services;
using Proton.Drive.App.Windows.SystemIntegration;
using Proton.Drive.Sdk.Sync.Agent.Mapping;
using Proton.Drive.Sdk.Sync.Agent.Mapping.SyncFolders;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Shared.Features;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Windows.Views.Main.MyComputer;

internal sealed class FolderListViewModel : ObservableObject, ISyncFoldersAware, IFeatureFlagsAware
{
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ISyncFolderService _syncFolderService;
    private readonly ILocalFolderService _localFolderService;
    private readonly IDialogService _dialogService;
    private readonly Func<AddFoldersViewModel> _addFoldersViewModelFactory;
    private readonly Func<RemoveClassicFolderConfirmationViewModel> _removeClassicFolderConfirmationViewModelFactory;
    private readonly Func<RemoveOnDemandFolderConfirmationViewModel> _removeOnDemandFolderConfirmationViewModelFactory;
    private readonly Func<StorageOptimizationTurnedOffNotificationViewModel> _storageOptimizationTurnedOffNotificationViewModelFactory;
    private readonly Func<StorageOptimizationUnavailableNotificationViewModel> _storageOptimizationUnavailableNotificationViewModelFactory;
    private readonly IScheduler _scheduler;

    private bool _isStorageOptimizationFeatureEnabled = true;
    private bool _isShowingStorageOptimizationNotificationDialog;

    public FolderListViewModel(
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ISyncFolderService syncFolderService,
        ILocalFolderService localFolderService,
        IDialogService dialogService,
        Func<AddFoldersViewModel> addFoldersViewModelFactory,
        Func<RemoveClassicFolderConfirmationViewModel> removeClassicFolderConfirmationViewModelFactory,
        Func<RemoveOnDemandFolderConfirmationViewModel> removeOnDemandFolderConfirmationViewModelFactory,
        Func<StorageOptimizationTurnedOffNotificationViewModel> storageOptimizationTurnedOffNotificationViewModelFactory,
        Func<StorageOptimizationUnavailableNotificationViewModel> storageOptimizationUnavailableNotificationViewModelFactory,
        [FromKeyedServices("Dispatcher")] IScheduler scheduler)
    {
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _syncFolderService = syncFolderService;
        _localFolderService = localFolderService;
        _dialogService = dialogService;
        _addFoldersViewModelFactory = addFoldersViewModelFactory;
        _removeClassicFolderConfirmationViewModelFactory = removeClassicFolderConfirmationViewModelFactory;
        _removeOnDemandFolderConfirmationViewModelFactory = removeOnDemandFolderConfirmationViewModelFactory;
        _storageOptimizationTurnedOffNotificationViewModelFactory = storageOptimizationTurnedOffNotificationViewModelFactory;
        _storageOptimizationUnavailableNotificationViewModelFactory = storageOptimizationUnavailableNotificationViewModelFactory;
        _scheduler = scheduler;

        AddFoldersCommand = new RelayCommand(AddFolders);
        OpenFolderCommand = new AsyncRelayCommand<FolderViewModel?>(OpenFolderAsync);
        RemoveFolderCommand = new AsyncRelayCommand<FolderViewModel?>(RemoveFolderAsync);
        ToggleStorageOptimizationCommand = new AsyncRelayCommand<FolderViewModel?>(ToggleStorageOptimizationAsync, CanToggleStorageOptimization);
    }

    public ObservableCollection<FolderViewModel> Items { get; } = [];

    public ICommand AddFoldersCommand { get; }
    public IAsyncRelayCommand<FolderViewModel?> OpenFolderCommand { get; }
    public IAsyncRelayCommand<FolderViewModel?> RemoveFolderCommand { get; }
    public IAsyncRelayCommand<FolderViewModel?> ToggleStorageOptimizationCommand { get; }

    public bool IsStorageOptimizationFeatureEnabled
    {
        get => _isStorageOptimizationFeatureEnabled;
        private set => SetProperty(ref _isStorageOptimizationFeatureEnabled, value);
    }

    void IFeatureFlagsAware.OnFeatureFlagsChanged(IReadOnlyDictionary<Feature, bool> features)
    {
        IsStorageOptimizationFeatureEnabled = !features[Feature.DriveWindowsStorageOptimizationDisabled];
        Schedule(ToggleStorageOptimizationCommand.NotifyCanExecuteChanged);
    }

    void ISyncFoldersAware.OnSyncFolderChanged(SyncFolderChangeType changeType, SyncFolder folder)
    {
        if (folder.Type is not SyncFolderType.HostDeviceFolder)
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

                    if (!_fileSystemDisplayNameAndIconProvider.TryGetDisplayNameAndIcon(folder.LocalPath, ShellIconSize.Small, out var name, out var icon))
                    {
                        name = _fileSystemDisplayNameAndIconProvider.GetDisplayNameWithoutAccess(folder.LocalPath) ?? string.Empty;
                        icon = _fileSystemDisplayNameAndIconProvider.GetFolderIconWithoutAccess(folder.LocalPath, ShellIconSize.Small);
                    }

                    Items.Add(new FolderViewModel(folder, name, icon));
                    break;

                case SyncFolderChangeType.Updated:
                    var item = Items.FirstOrDefault(syncedFolder => syncedFolder.Equals(folder));
                    HandleStorageOptimizationUnavailability(item, folder);
                    HandleStorageOptimizationTurnedOff(item, folder);
                    item?.Update();
                    ToggleStorageOptimizationCommand.NotifyCanExecuteChanged();
                    break;

                case SyncFolderChangeType.Removed:
                    Items.RemoveFirst(syncedFolder => syncedFolder.Equals(folder));
                    break;

                default:
                    throw new InvalidEnumArgumentException(nameof(changeType), (int)changeType, typeof(SyncFolderChangeType));
            }
        }
    }

    private void HandleStorageOptimizationUnavailability(FolderViewModel? folderViewModel, SyncFolder syncFolder)
    {
        if (_isShowingStorageOptimizationNotificationDialog)
        {
            return;
        }

        // The folder view model is not yet updated to reflect the latest status of SyncFolder,
        // so we get the previous state from the view model and the new state from the SyncFolder.
        if (folderViewModel?.StorageOptimizationStatus is not StorageOptimizationStatus.Pending || !folderViewModel.IsStorageOptimizationEnabled)
        {
            return;
        }

        if (syncFolder is not
            {
                Status: MappingSetupStatus.Succeeded,
                SyncMethod: SyncMethod.Classic,
                IsStorageOptimizationEnabled: false,
                StorageOptimizationErrorCode: not StorageOptimizationErrorCode.None,
            })
        {
            return;
        }

        var dialogViewModel = _storageOptimizationUnavailableNotificationViewModelFactory.Invoke();
        dialogViewModel.SetArguments(folderViewModel.Name, syncFolder.StorageOptimizationErrorCode, syncFolder.ConflictingProviderName);

        try
        {
            _isShowingStorageOptimizationNotificationDialog = true;
            _dialogService.ShowConfirmationDialog(dialogViewModel);
        }
        finally
        {
            _isShowingStorageOptimizationNotificationDialog = false;
        }
    }

    private void HandleStorageOptimizationTurnedOff(FolderViewModel? folderViewModel, SyncFolder syncFolder)
    {
        if (_isShowingStorageOptimizationNotificationDialog)
        {
            return;
        }

        // The folder view model is not yet updated to reflect the latest status of SyncFolder,
        // so we get the previous state from the view model and the new state from the SyncFolder.
        if (folderViewModel?.StorageOptimizationStatus is not StorageOptimizationStatus.Pending || folderViewModel.IsStorageOptimizationEnabled)
        {
            return;
        }

        if (syncFolder is not
            {
                Status: MappingSetupStatus.Succeeded,
                SyncMethod: SyncMethod.OnDemand,
                IsStorageOptimizationEnabled: false,
                StorageOptimizationErrorCode: StorageOptimizationErrorCode.None,
            })
        {
            return;
        }

        var dialogViewModel = _storageOptimizationTurnedOffNotificationViewModelFactory.Invoke();
        dialogViewModel.SetArguments(folderViewModel.Name);

        try
        {
            _isShowingStorageOptimizationNotificationDialog = true;
            _dialogService.ShowConfirmationDialog(dialogViewModel);
        }
        finally
        {
            _isShowingStorageOptimizationNotificationDialog = false;
        }
    }

    private void AddFolders()
    {
        var dialogViewModel = _addFoldersViewModelFactory.Invoke();
        dialogViewModel.RefreshSyncedFolders(Items.Select(x => x.Path).ToHashSet());
        _dialogService.ShowDialog(dialogViewModel);
    }

    private Task OpenFolderAsync(FolderViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return Task.CompletedTask;
        }

        return _localFolderService.OpenFolderAsync(viewModel.Path);
    }

    private Task RemoveFolderAsync(FolderViewModel? viewModel, CancellationToken cancellationToken)
    {
        if (viewModel is null)
        {
            return Task.CompletedTask;
        }

        var confirmationViewModel = GetConfirmationViewModel(viewModel.DataItem.SyncMethod, viewModel.Name);

        var confirmationResult = _dialogService.ShowConfirmationDialog(confirmationViewModel);

        if (confirmationResult != ConfirmationResult.Confirmed)
        {
            return Task.CompletedTask;
        }

        return _syncFolderService.RemoveHostDeviceFolderAsync(viewModel.DataItem, cancellationToken);
    }

    private RemoveFolderConfirmationViewModelBase GetConfirmationViewModel(SyncMethod syncMethod, string folderName)
    {
        RemoveFolderConfirmationViewModelBase viewModel = syncMethod switch
        {
            SyncMethod.Classic => _removeClassicFolderConfirmationViewModelFactory.Invoke(),
            SyncMethod.OnDemand => _removeOnDemandFolderConfirmationViewModelFactory.Invoke(),
            _ => throw new ArgumentOutOfRangeException(nameof(syncMethod), syncMethod, message: null),
        };

        viewModel.SetFolderName(folderName);

        return viewModel;
    }

    private bool CanToggleStorageOptimization(FolderViewModel? viewModel)
    {
        return
            IsStorageOptimizationFeatureEnabled &&
            viewModel?.Status is MappingSetupStatus.Succeeded;
    }

    private Task ToggleStorageOptimizationAsync(FolderViewModel? viewModel, CancellationToken cancellationToken)
    {
        if (viewModel is null)
        {
            return Task.CompletedTask;
        }

        return _syncFolderService.SetStorageOptimizationAsync(viewModel.DataItem, !viewModel.IsStorageOptimizationEnabled, cancellationToken);
    }

    private void Schedule(Action action)
    {
        _scheduler.Schedule(action);
    }
}
