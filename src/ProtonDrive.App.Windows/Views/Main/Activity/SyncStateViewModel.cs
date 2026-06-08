using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Sync;
using ProtonDrive.App.Update;
using ProtonDrive.App.Windows.Services;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Configuration;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.ExecutionStatistics;
using ProtonDrive.Sync.Shared.FileSystem.Integration;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Windows.Views.Main.Activity;

internal sealed class SyncStateViewModel
    : PageViewModel, ISessionStateAware, ISyncStateAware, ISyncActivityAware, ISyncStatisticsAware, IDisposable
{
    public const int MaxNumberOfVisibleItems = 100;

    private static readonly TimeSpan IntervalOfSyncedTimeUpdate = TimeSpan.FromSeconds(40);

    private readonly ISyncService _syncService;
    private readonly ObservableCollection<SyncActivityItemViewModel> _syncActivityItems = [];
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ILocalFolderService _localFolderService;
    private readonly IScheduler _scheduler;
    private readonly IDialogService _dialogService;
    private readonly RenameRemoteNodeViewModel _renameRemoteNodeDialogViewModel;
    private readonly IClock _clock;

    private readonly AsyncRelayCommand _retrySyncCommand;
    private readonly RelayCommand<SyncActivityItemViewModel> _fixItemNameCommand;
    private readonly RelayCommand _updateNowCommand;
    private readonly ISchedulerTimer _timer;
    private readonly TimeSpan _delayBeforeDisplayingSyncInitializationProgress;

    private SyncState _synchronizationState = SyncState.Terminated;
    private bool _isSyncInitialized;
    private bool _isNewSession;
    private int _latestSyncPassNumber;
    private DateTime? _syncInitializationStartTime;
    private bool _updateIsReady;

    public SyncStateViewModel(
        ISyncService syncService,
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ILocalFolderService localFolderService,
        [FromKeyedServices("Dispatcher")] IScheduler scheduler,
        AppConfig appConfig,
        IDialogService dialogService,
        RenameRemoteNodeViewModel renameRemoteNodeDialogViewModel,
        IUpdateService updateService,
        IClock clock)
    {
        _syncService = syncService;
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _localFolderService = localFolderService;
        _scheduler = scheduler;
        _dialogService = dialogService;
        _renameRemoteNodeDialogViewModel = renameRemoteNodeDialogViewModel;
        _clock = clock;
        _delayBeforeDisplayingSyncInitializationProgress = appConfig.DelayBeforeDisplayingSyncInitializationProgress;

        _retrySyncCommand = new AsyncRelayCommand(RetrySyncAsync, CanRetrySync);
        _fixItemNameCommand = new RelayCommand<SyncActivityItemViewModel>(OpenRenameItemDialog);
        _updateNowCommand = new RelayCommand(updateService.StartUpdating, CanUpdate);

        updateService.StateChanged += OnUpdateStateChanged;

        SyncActivityItems = GetItems();
        FailedItems = GetFailedItems();

        _timer = _scheduler.CreateTimer();
        _timer.Tick += OnTimerTick;
        _timer.Interval = IntervalOfSyncedTimeUpdate;
        _timer.Start();
    }

    public SyncStatus SynchronizationStatus => _synchronizationState.Status;

    public ICommand UpdateNowCommand => _updateNowCommand;

    public bool UpdateIsRequired
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool IsDisplayingDetails
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsInitializingForTheFirstTime
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool Paused
    {
        get => _syncService.Paused;
        set
        {
            if (_syncService.Paused == value)
            {
                return;
            }

            _syncService.Paused = value;
            OnPropertyChanged();
        }
    }

    public int? NumberOfInitializedItems
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public ICommand RetrySyncCommand => _retrySyncCommand;

    public ICommand FixItemNameCommand => _fixItemNameCommand;

    public ListCollectionView SyncActivityItems { get; }

    public ListCollectionView FailedItems { get; }

    public void Dispose()
    {
        _timer.Dispose();
    }

    void ISessionStateAware.OnSessionStateChanged(SessionState value)
    {
        if (value.Status is SessionStatus.Starting or SessionStatus.SigningIn)
        {
            _isNewSession = true;
        }
    }

    void ISyncStateAware.OnSyncStateChanged(SyncState value)
    {
        _scheduler.Schedule(
            () =>
            {
                _synchronizationState = value;

                _isSyncInitialized = value.Status switch
                {
                    SyncStatus.Idle or SyncStatus.Synchronizing or SyncStatus.Failed => true,
                    SyncStatus.Terminating or SyncStatus.Terminated => false,
                    _ => _isSyncInitialized,
                };

                IsInitializingForTheFirstTime = !_isSyncInitialized && value.Status is SyncStatus.Initializing or SyncStatus.DetectingUpdates;

                if (IsInitializingForTheFirstTime)
                {
                    _syncInitializationStartTime ??= _clock.UtcNow;
                }
                else
                {
                    _syncInitializationStartTime = null;
                }

                OnPropertyChanged(nameof(SynchronizationStatus));
                OnPropertyChanged(nameof(Paused));

                if (_isNewSession && value.Status is SyncStatus.Terminated or SyncStatus.Initializing)
                {
                    _isNewSession = false;
                    _syncActivityItems.Clear();
                }

                switch (value.Status)
                {
                    case SyncStatus.Synchronizing:
                        ++_latestSyncPassNumber;
                        break;

                    case SyncStatus.Idle:
                        RemoveOutdatedFailedItems();
                        break;
                }
            });
    }

    void ISyncActivityAware.OnSyncActivityChanged(SyncActivityItem<long> item)
    {
        _scheduler.Schedule(
            () =>
            {
                var itemViewModel = _syncActivityItems.FirstOrDefault(
                    x => x.DataItem.Id == item.Id && x.DataItem.Replica == item.Replica && x.DataItem.Source == item.Source);

                if (itemViewModel is null)
                {
                    AddNewItem();
                }
                else
                {
                    UpdateExistingItem(itemViewModel);
                }
            });

        return;

        void AddNewItem()
        {
            if (item.Status is not SyncActivityItemStatus.InProgress)
            {
                return;
            }

            var itemViewModel = new SyncActivityItemViewModel(item, _fileSystemDisplayNameAndIconProvider, _localFolderService, _latestSyncPassNumber);

            _syncActivityItems.Add(itemViewModel);

            if (_syncActivityItems.Count > MaxNumberOfVisibleItems)
            {
                SyncActivityItems.RemoveAt(MaxNumberOfVisibleItems);
            }
        }

        void UpdateExistingItem(SyncActivityItemViewModel itemViewModel)
        {
            itemViewModel.LastSyncPassNumber = _latestSyncPassNumber;

            // Skipped sync activity item does not carry all property values, it cannot be used for displaying purposes
            if (item.Status is SyncActivityItemStatus.Skipped)
            {
                return;
            }

            // Success sync activity item might not carry all property values, when state-based update detection reports success.
            // We assume properties have not changed.
            if (item.Status is SyncActivityItemStatus.Succeeded &&
                string.IsNullOrEmpty(item.Name) &&
                string.IsNullOrEmpty(item.LocalRootPath))
            {
                item = itemViewModel.DataItem with
                {
                    ErrorCode = default,
                    ErrorMessage = null,
                    Status = SyncActivityItemStatus.Succeeded,
                };
            }

            itemViewModel.DataItem = item;

            if (item.Status is SyncActivityItemStatus.InProgress)
            {
                itemViewModel.SynchronizedAt = null;
            }
            else if (itemViewModel.SynchronizedAt == null)
            {
                itemViewModel.SynchronizedAt = _clock.UtcNow;
            }
        }
    }

    void ISyncStatisticsAware.OnSyncStatisticsChanged(IExecutionStatistics value)
    {
        _scheduler.Schedule(
            () =>
            {
                if (_synchronizationState.Status is not (SyncStatus.Initializing or SyncStatus.DetectingUpdates))
                {
                    NumberOfInitializedItems = null;
                    return;
                }

                if (_clock.UtcNow - _syncInitializationStartTime > _delayBeforeDisplayingSyncInitializationProgress)
                {
                    NumberOfInitializedItems = value.Succeeded + value.Failed;
                }
            });
    }

    private static bool ItemSyncHasFailed(object item)
    {
        return item is SyncActivityItemViewModel { Status: SyncActivityItemStatus.Failed or SyncActivityItemStatus.Warning };
    }

    private bool CanUpdate()
    {
        return _updateIsReady;
    }

    private void OnUpdateStateChanged(object? sender, UpdateState e)
    {
        _scheduler.Schedule(() =>
        {
            UpdateIsRequired = e.UpdateRequired;
            _updateIsReady = e.IsReady;
            _updateNowCommand.NotifyCanExecuteChanged();
        });
    }

    private void RemoveOutdatedFailedItems()
    {
        for (int index = _syncActivityItems.Count - 1; index >= 0; --index)
        {
            var item = _syncActivityItems[index];

            if (item.LastSyncPassNumber < _latestSyncPassNumber && (item.Status is SyncActivityItemStatus.Cancelled || ItemSyncHasFailed(item)))
            {
                _syncActivityItems.RemoveAt(index);
            }
        }
    }

    private ListCollectionView GetItems()
    {
        return new ListCollectionView(_syncActivityItems)
        {
            LiveSortingProperties = { nameof(SyncActivityItemViewModel.Status), nameof(SyncActivityItemViewModel.SynchronizedAt) },
            IsLiveSorting = true,
            CustomSort = new SyncActivityItemComparer(),
        };
    }

    private ListCollectionView GetFailedItems()
    {
        var failedItemsView = new ListCollectionView(_syncActivityItems)
        {
            LiveFilteringProperties = { nameof(SyncActivityItemViewModel.Status) },
            IsLiveFiltering = true,
            Filter = ItemSyncHasFailed,
        };

        ((INotifyPropertyChanged)failedItemsView).PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not nameof(CollectionView.Count))
            {
                return;
            }

            _retrySyncCommand.NotifyCanExecuteChanged();
        };

        return failedItemsView;
    }

    private bool CanRetrySync()
    {
        return FailedItems.Count > 0;
    }

    private async Task RetrySyncAsync()
    {
        Paused = true;

        await Task.Delay(300, CancellationToken.None).ConfigureAwait(true);

        Paused = false;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        foreach (var item in _syncActivityItems)
        {
            item.OnSynchronizedAtChanged();
        }
    }

    private void OpenRenameItemDialog(SyncActivityItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        _renameRemoteNodeDialogViewModel.SetSyncActivityItem(item);
        _dialogService.ShowDialog(_renameRemoteNodeDialogViewModel);
    }

    private sealed class SyncActivityItemComparer : IComparer
    {
        int IComparer.Compare(object? x, object? y)
        {
#pragma warning disable RCS1256
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);
#pragma warning restore RCS1256

            return Compare((SyncActivityItemViewModel)x, (SyncActivityItemViewModel)y);
        }

        private static int Compare(SyncActivityItemViewModel x, SyncActivityItemViewModel y)
        {
            // Display in-progress operations first
            if (x.Status is SyncActivityItemStatus.InProgress)
            {
                return -1;
            }

            // Display latest synced items first
            return (y.SynchronizedAt ?? DateTime.MaxValue).CompareTo(x.SynchronizedAt ?? DateTime.MaxValue);
        }
    }
}
