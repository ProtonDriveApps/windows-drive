using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ProtonDrive.App.Drive.Services.Shared;
using ProtonDrive.App.Drive.Services.SharedWithMe;
using ProtonDrive.App.Mapping;
using ProtonDrive.App.Mapping.SyncFolders;
using ProtonDrive.App.Sync;
using ProtonDrive.App.Windows.Extensions;
using ProtonDrive.App.Windows.Toolkit.Threading;
using ProtonDrive.App.Windows.Views.Shared.Sorting;
using ProtonDrive.Client;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Client.Shares.SharedWithMe;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Features;
using ProtonDrive.Sync.Shared.FileSystem.Integration;
using ProtonDrive.Sync.Shared.SyncActivity;
using SharedWithMeItem = ProtonDrive.Client.Shares.SharedWithMe.SharedWithMeItem;

namespace ProtonDrive.App.Windows.Views.Main.SharedWithMe;

internal sealed class SharedWithMeListViewModel : SortableListViewModel, ISyncFoldersAware, IFeatureFlagsAware, ISyncStateAware
{
    private readonly ISharedWithMeDataProvider _dataProvider;
    private readonly ISharedWithMeClient _sharedWithMeClient;
    private readonly IFeatureFlagProvider _featureFlagProvider;
    private readonly ILocalFolderService _localFolderService;
    private readonly ISharedWithMeMappingService _sharedWithMeMappingService;
    private readonly DispatcherScheduler _scheduler;
    private readonly SharedWithMeItemViewModelFactory _itemViewModelFactory;
    private readonly ILogger<SharedWithMeListViewModel> _logger;

    private readonly Lazy<Task> _initializeData;
    private readonly AsyncRelayCommand _openSharedWithMeRootFolderCommand;
    private readonly AsyncRelayCommand<SharedWithMeItemViewModel?> _toggleSyncCommand;
    private readonly AsyncRelayCommand<SharedWithMeItemViewModel?> _removeMeCommand;
    private readonly TimeSpan _refreshCooldownDuration = TimeSpan.FromSeconds(30);

    private readonly ObservableCollection<SharedWithMeItemViewModel> _items = [];

    private DataServiceStatus _status;
    private DateTime _lastRefreshTime = DateTime.MinValue;
    private int _numberOfFailedItems;
    private int _numberOfSyncedItems;
    private bool _maximumNumberOfSyncedFoldersReached;
    private bool _isFeatureDisabled;
    private bool _syncIsRestarting;

    public SharedWithMeListViewModel(
        AppConfig config,
        ISharedWithMeDataProvider dataProvider,
        ISharedWithMeClient sharedWithMeClient,
        IFeatureFlagProvider featureFlagProvider,
        ILocalFolderService localFolderService,
        ISharedWithMeMappingService sharedWithMeMappingService,
        DispatcherScheduler scheduler,
        SharedWithMeItemViewModelFactory itemViewModelFactory,
        ILogger<SharedWithMeListViewModel> logger)
    {
        // This limitation is necessary to minimize the number of polling requests,
        // ensuring the folders remain synchronized efficiently.
        MaximumNumberOfSyncedItemsSupported = config.MaxNumberOfSyncedSharedWithMeItems;

        _dataProvider = dataProvider;
        _sharedWithMeClient = sharedWithMeClient;
        _featureFlagProvider = featureFlagProvider;
        _localFolderService = localFolderService;
        _sharedWithMeMappingService = sharedWithMeMappingService;
        _scheduler = scheduler;
        _itemViewModelFactory = itemViewModelFactory;
        _logger = logger;

        _openSharedWithMeRootFolderCommand = new AsyncRelayCommand(OpenSharedWithMeRootFolder, CanOpenSharedWithMeRootFolder);
        _toggleSyncCommand = new AsyncRelayCommand<SharedWithMeItemViewModel?>(ToggleSync, CanToggleSync);
        _removeMeCommand = new AsyncRelayCommand<SharedWithMeItemViewModel?>(RemoveMeAsync, CanRemoveMe);

        _initializeData = new Lazy<Task>(() => InitializeAsync(CancellationToken.None));

        GridColumns = new SharedWithMeGridColumnsViewModel();

        Items = new ListCollectionView(_items)
        {
            IsLiveSorting = true,

            // We do not enable live sorting of Sync column, so that list entries
            // preserve their vertical position upon enabling or disabling sync.
            LiveSortingProperties =
            {
                GridColumns.PermissionsColumn.Name,
                GridColumns.NameColumn.Name,
                GridColumns.SharedByColumn.Name,
                GridColumns.SharedOnColumn.Name,
            },
        };

        SortItems(GridColumns.SharedOnColumn, ListSortDirection.Descending);
    }

    public ListCollectionView Items { get; }

    public DataServiceStatus Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public int NumberOfFailedItems
    {
        get => _numberOfFailedItems;
        private set => SetProperty(ref _numberOfFailedItems, value);
    }

    public int NumberOfSyncedItems
    {
        get => _numberOfSyncedItems;
        private set
        {
            if (SetProperty(ref _numberOfSyncedItems, value))
            {
                MaximumNumberOfSyncedFoldersReached = value >= MaximumNumberOfSyncedItemsSupported;
                _toggleSyncCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool MaximumNumberOfSyncedFoldersReached
    {
        get => _maximumNumberOfSyncedFoldersReached;
        set => SetProperty(ref _maximumNumberOfSyncedFoldersReached, value);
    }

    public bool IsFeatureDisabled
    {
        get => _isFeatureDisabled;
        private set
        {
            if (SetProperty(ref _isFeatureDisabled, value))
            {
                _toggleSyncCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public int MaximumNumberOfSyncedItemsSupported { get; }

    public ICommand OpenSharedWithMeRootFolderCommand => _openSharedWithMeRootFolderCommand;

    public SharedWithMeGridColumnsViewModel GridColumns { get; }

    public async Task LoadDataAsync()
    {
        _logger.LogDebug("Loading shared with me data...");

        await _initializeData.Value.ConfigureAwait(true);

        // TODO:
        // Replace this workaround when global and volume-based events are implemented.
        // Its purpose is to mitigate potential server strain caused by frequent refreshes.
        var isCooldownActive = DateTime.UtcNow - _lastRefreshTime < _refreshCooldownDuration;
        if (isCooldownActive && Items.Count > 0)
        {
            _logger.LogDebug("Cooldown active, skip shared with me data refresh");
            return;
        }

        _lastRefreshTime = DateTime.UtcNow;

        await RefreshFeatureFlagAsync().ConfigureAwait(true);

        _dataProvider.RequestRefresh();
    }

    void ISyncFoldersAware.OnSyncFolderChanged(SyncFolderChangeType changeType, SyncFolder folder)
    {
        Schedule(
            () =>
            {
                if (folder.Type is not SyncFolderType.SharedWithMeItem)
                {
                    return;
                }

                HandleSyncFolderChange(changeType, folder);
            });
    }

    void IFeatureFlagsAware.OnFeatureFlagsChanged(IReadOnlyDictionary<Feature, bool> features)
    {
        Schedule(
            () =>
                IsFeatureDisabled = features[Feature.DriveSharingDisabled] || features[Feature.DriveSharingEditingDisabled]);
    }

    void ISyncStateAware.OnSyncStateChanged(SyncState value)
    {
        Schedule(
            () =>
            {
                _syncIsRestarting = value.Status is SyncStatus.Initializing or SyncStatus.Terminating;
                _toggleSyncCommand.NotifyCanExecuteChanged();
            });
    }

    protected override void SortItems(GridColumnViewModel? column)
    {
        if (column is null)
        {
            return;
        }

        var sortDirection = column.SortDirection is ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        SortItems(column, sortDirection);
    }

    private static SharedWithMeItem ToSharedWithMeItem(SyncFolder syncFolder)
    {
        return new SharedWithMeItem
        {
            Id = syncFolder.RemoteShareId ?? throw new InvalidOperationException("Remote share ID cannot be null"),
            LinkId = string.Empty,
            VolumeId = string.Empty,
            IsFolder = syncFolder.Type is SyncFolderType.SharedWithMeItem && syncFolder.RootLinkType is LinkType.Folder,
            Name = syncFolder.RemoteName ?? string.Empty,
            SharingTime = default,
        };
    }

    private static bool CanRemoveMe(SharedWithMeItemViewModel? item)
    {
        return item?.DataItem?.MemberId is not null && item.Status is MappingSetupStatus.None;
    }

    private void HandleSyncFolderChange(SyncFolderChangeType changeType, SyncFolder folder)
    {
        switch (changeType)
        {
            case SyncFolderChangeType.Added:
                ++NumberOfSyncedItems;
                HandleAddedOrUpdatedSyncFolder(folder);
                break;

            case SyncFolderChangeType.Updated:
                HandleAddedOrUpdatedSyncFolder(folder);
                break;

            case SyncFolderChangeType.Removed:
                --NumberOfSyncedItems;
                HandleRemovedSyncFolder(folder);
                break;
        }

        _openSharedWithMeRootFolderCommand.NotifyCanExecuteChanged();
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshFeatureFlagAsync().ConfigureAwait(true);

            var dataItems = await _dataProvider.GetItemsAsync(cancellationToken).ConfigureAwait(true);

            _dataProvider.StateChanged += OnDataProviderStateChanged;
            OnDataProviderStateChanged(_dataProvider, _dataProvider.State);

            using (dataItems)
            {
                _items.AddEach(dataItems.Select(CreateItemViewModel));

                _dataProvider.ItemsChanged += OnDataItemsChanged;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    private void OnDataProviderStateChanged(object? sender, DataServiceState value)
    {
        Status = value.Status;
        NumberOfFailedItems = value.NumberOfFailedItems;
    }

    private void OnDataItemsChanged(object? sender, ItemsChangedEventArgs<string, SharedWithMeItem> args)
    {
        Schedule(
            () =>
            {
                if (args.ChangeType is ItemsChangeType.Cleared)
                {
                    HandleClearedItems();
                }
                else
                {
                    HandleDataItemChange(args.ChangeType, args.Item);
                }
            });
    }

    private void HandleDataItemChange(ItemsChangeType changeType, SharedWithMeItem dataItem)
    {
        switch (changeType)
        {
            case ItemsChangeType.Added:
            case ItemsChangeType.Updated:
                HandleAddedOrUpdatedItem(dataItem);
                break;

            case ItemsChangeType.Removed:
                HandleRemovedItem(dataItem);
                break;
        }
    }

    private void HandleClearedItems()
    {
        if (_items.All(i => i.SyncFolder == null))
        {
            _items.Clear();
        }
        else
        {
            for (var i = Items.Count - 1; i >= 0; i--)
            {
                if (_items[i].SyncFolder == null)
                {
                    _items.RemoveAt(i);
                }
                else
                {
                    _items[i].DataItem = null;
                }
            }
        }
    }

    private void HandleAddedOrUpdatedItem(SharedWithMeItem dataItem)
    {
        var item = _items.FirstOrDefault(i => i.Id.Equals(dataItem.Id));

        if (item is null)
        {
            _items.Add(CreateItemViewModel(dataItem));
        }
        else
        {
            item.DataItem = dataItem;
        }
    }

    private void HandleRemovedItem(SharedWithMeItem dataItem)
    {
        var item = _items.FirstOrDefault(i => i.Id.Equals(dataItem.Id));
        if (item is null)
        {
            // Item already removed due to lack of coordination
            return;
        }

        if (item.SyncFolder is null)
        {
            _items.Remove(item);
        }
        else
        {
            item.DataItem = null;
        }
    }

    private void HandleAddedOrUpdatedSyncFolder(SyncFolder syncFolder)
    {
        var item = _items.FirstOrDefault(i => i.SyncFolder == syncFolder);

        if (item == null && !string.IsNullOrEmpty(syncFolder.RemoteShareId))
        {
            item = _items.FirstOrDefault(i => i.Id.Equals(syncFolder.RemoteShareId));
        }

        if (item == null)
        {
            _items.Add(CreateItemViewModel(syncFolder));
        }
        else
        {
            item.SyncFolder = syncFolder;
        }
    }

    private void HandleRemovedSyncFolder(SyncFolder syncFolder)
    {
        var item = _items.FirstOrDefault(i => i.SyncFolder == syncFolder);
        if (item is null)
        {
            // Item already removed
            return;
        }

        if (item.DataItem is null)
        {
            _items.Remove(item);
        }
        else
        {
            item.SyncFolder = null;
        }
    }

    private SharedWithMeItemViewModel CreateItemViewModel(SharedWithMeItem dataItem)
    {
        return _itemViewModelFactory.Create(dataItem, _toggleSyncCommand, _removeMeCommand);
    }

    private SharedWithMeItemViewModel CreateItemViewModel(SyncFolder syncFolder)
    {
        return _itemViewModelFactory.Create(syncFolder, _toggleSyncCommand, _removeMeCommand);
    }

    private void SortItems(GridColumnViewModel column, ListSortDirection sortDirection)
    {
        GridColumns.ClearSorting();

        column.SortDirection = sortDirection;

        Items.SortDescriptions.Clear();
        Items.SortDescriptions.Add(new SortDescription(column.Name, sortDirection));
    }

    private bool CanToggleSync(SharedWithMeItemViewModel? itemViewModel)
    {
        if (itemViewModel?.SetupIsInProgress != false || _syncIsRestarting)
        {
            return false;
        }

        if (itemViewModel.IsSyncEnabled)
        {
            return true; // Disabling sync folder should always be allowed
        }

        if (IsFeatureDisabled)
        {
            return false;
        }

        return !MaximumNumberOfSyncedFoldersReached;
    }

    private async Task ToggleSync(SharedWithMeItemViewModel? itemViewModel, CancellationToken cancellationToken)
    {
        try
        {
            switch (itemViewModel)
            {
                case null:
                    return;

                case { IsSyncEnabled: false, DataItem: not null }:
                    await _sharedWithMeMappingService.AddSharedWithMeItemAsync(itemViewModel.DataItem, cancellationToken).ConfigureAwait(true);
                    break;

                case { IsSyncEnabled: true, SyncFolder: not null }:
                    var dataItem = itemViewModel.DataItem ?? ToSharedWithMeItem(itemViewModel.SyncFolder);

                    await _sharedWithMeMappingService.RemoveSharedWithMeItemAsync(dataItem, cancellationToken).ConfigureAwait(true);
                    break;
            }
        }
        finally
        {
            _openSharedWithMeRootFolderCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task RemoveMeAsync(SharedWithMeItemViewModel? itemViewModel, CancellationToken cancellationToken)
    {
        if (itemViewModel?.DataItem?.MemberId is null)
        {
            return;
        }

        if (itemViewModel.Status is not MappingSetupStatus.None)
        {
            return;
        }

        try
        {
            await _sharedWithMeClient.RemoveMemberAsync(
                shareId: itemViewModel.DataItem.Id,
                itemViewModel.DataItem.MemberId,
                cancellationToken).ConfigureAwait(true);

            Items.Remove(itemViewModel);
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            _logger.LogWarning("Failed to remove me from the shared with me item: {Message}", ex.Message);
        }
    }

    private async Task RefreshFeatureFlagAsync()
    {
        IsFeatureDisabled =
            await _featureFlagProvider.IsEnabledAsync(Feature.DriveSharingDisabled, CancellationToken.None).ConfigureAwait(true)
            || await _featureFlagProvider.IsEnabledAsync(Feature.DriveSharingEditingDisabled, CancellationToken.None).ConfigureAwait(true);
    }

    private bool CanOpenSharedWithMeRootFolder()
    {
        return _items.Any(x => x.SyncFolder is not null);
    }

    private async Task OpenSharedWithMeRootFolder()
    {
        var sharedWithMeFolder = _items.Select(x => x.SyncFolder).FirstOrDefault(x => x is not null);
        if (sharedWithMeFolder is null)
        {
            return;
        }

        var sharedWithMeRootFolderPath = Path.GetDirectoryName(sharedWithMeFolder.LocalPath);

        await _localFolderService.OpenFolderAsync(sharedWithMeRootFolderPath).ConfigureAwait(true);
    }

    private void Schedule(Action action)
    {
        _scheduler.Schedule(action);
    }
}
