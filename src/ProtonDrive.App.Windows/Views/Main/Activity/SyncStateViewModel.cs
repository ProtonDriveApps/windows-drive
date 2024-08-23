using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ProtonDrive.App.Authentication;
using ProtonDrive.App.Sync;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.App.Windows.SystemIntegration;
using ProtonDrive.Shared.Threading;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Windows.Views.Main.Activity;

internal sealed class SyncStateViewModel : PageViewModel, ISessionStateAware, ISyncStateAware, ISyncActivityAware, IDisposable
{
    public const int MaxNumberOfVisibleItems = 100;

    private static readonly TimeSpan IntervalOfSyncedTimeUpdate = TimeSpan.FromSeconds(40);

    private readonly ISyncService _syncService;
    private readonly ObservableCollection<SyncActivityItemViewModel> _syncActivityItems = [];
    private readonly IFileSystemDisplayNameAndIconProvider _fileSystemDisplayNameAndIconProvider;
    private readonly ILocalFolderService _localFolderService;
    private readonly IScheduler _scheduler;

    private readonly AsyncRelayCommand _retrySyncCommand;
    private readonly ISchedulerTimer _timer;

    private SyncState _synchronizationState = SyncState.Terminated;
    private bool _isSyncInitialized;
    private bool _isInitializingForTheFirstTime;
    private bool _isNewSession;
    private bool _isDisplayingDetails;

    public SyncStateViewModel(
        ISyncService syncService,
        IFileSystemDisplayNameAndIconProvider fileSystemDisplayNameAndIconProvider,
        ILocalFolderService localFolderService,
        [FromKeyedServices("Dispatcher")] IScheduler scheduler)
    {
        _syncService = syncService;
        _scheduler = scheduler;
        _fileSystemDisplayNameAndIconProvider = fileSystemDisplayNameAndIconProvider;
        _localFolderService = localFolderService;

        _retrySyncCommand = new AsyncRelayCommand(RetrySyncAsync, CanRetrySync);

        SyncActivityItems = GetItems();
        FailedItems = GetFailedItems();

        _timer = _scheduler.CreateTimer();
        _timer.Tick += OnTimerTick;
        _timer.Interval = IntervalOfSyncedTimeUpdate;
        _timer.Start();
    }

    public SyncStatus SynchronizationStatus => _synchronizationState.Status;

    public bool IsDisplayingDetails
    {
        get => _isDisplayingDetails;
        set => SetProperty(ref _isDisplayingDetails, value);
    }

    public bool IsInitializingForTheFirstTime
    {
        get => _isInitializingForTheFirstTime;
        private set => SetProperty(ref _isInitializingForTheFirstTime, value);
    }

    public bool Paused
    {
        get => _syncService.Paused;
        set
        {
            _syncService.Paused = value;
            OnPropertyChanged();
        }
    }

    public ICommand RetrySyncCommand => _retrySyncCommand;

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
        _scheduler.Schedule(() =>
        {
            _synchronizationState = value;

            _isSyncInitialized = value.Status switch
            {
                SyncStatus.Idle or SyncStatus.Synchronizing or SyncStatus.Failed => true,
                SyncStatus.Terminating or SyncStatus.Terminated => false,
                _ => _isSyncInitialized,
            };

            IsInitializingForTheFirstTime = !_isSyncInitialized && value.Status is SyncStatus.Initializing or SyncStatus.DetectingUpdates;

            OnPropertyChanged(nameof(SynchronizationStatus));
            OnPropertyChanged(nameof(Paused));

            if (_isNewSession && value.Status is SyncStatus.Terminated or SyncStatus.Initializing)
            {
                _isNewSession = false;
                _syncActivityItems.Clear();
            }
        });
    }

    void ISyncActivityAware.OnSyncActivityChanged(SyncActivityItem<long> item)
    {
        _scheduler.Schedule(() =>
        {
            var itemViewModel = _syncActivityItems.FirstOrDefault(x => x.DataItem.Id == item.Id);

            if (itemViewModel is null)
            {
                InsertNewItem();
            }
            else
            {
                UpdateExistingItem(itemViewModel);
            }
        });

        return;

        void InsertNewItem()
        {
            if (item.Status is not SyncActivityItemStatus.InProgress)
            {
                return;
            }

            var itemViewModel = new SyncActivityItemViewModel(item, _fileSystemDisplayNameAndIconProvider, _localFolderService);

            _syncActivityItems.Add(itemViewModel);

            if (_syncActivityItems.Count > MaxNumberOfVisibleItems)
            {
                SyncActivityItems.RemoveAt(MaxNumberOfVisibleItems);
            }
        }

        void UpdateExistingItem(SyncActivityItemViewModel itemViewModel)
        {
            itemViewModel.DataItem = item;

            if (item.Status is SyncActivityItemStatus.InProgress)
            {
                itemViewModel.SynchronizedAt = default;
            }

            if (item.Status is not SyncActivityItemStatus.InProgress && itemViewModel.SynchronizedAt == default)
            {
                itemViewModel.SynchronizedAt = DateTime.UtcNow;
            }
        }
    }

    private static bool ItemIsNotSkipped(object item)
    {
        return item is not SyncActivityItemViewModel { Status: SyncActivityItemStatus.Skipped };
    }

    private static bool ItemSyncHasFailed(object item)
    {
        return item is SyncActivityItemViewModel { Status: SyncActivityItemStatus.Failed or SyncActivityItemStatus.Warning };
    }

    private ListCollectionView GetItems()
    {
        return new ListCollectionView(_syncActivityItems)
        {
            LiveFilteringProperties = { nameof(SyncActivityItemViewModel.Status) },
            IsLiveFiltering = true,
            Filter = ItemIsNotSkipped,
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
