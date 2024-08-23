using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Client;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.App.Drive.Services.Shared;

internal abstract class StateBasedUpdateDetectorBase<TKey, TItem>
    where TKey : IEquatable<TKey>
    where TItem : IIdentifiable<TKey>
{
    private readonly HashSet<TKey> _dirtyKeys = [];

    protected StateBasedUpdateDetectorBase(
        LockableObservableDataSet<TKey, TItem> dataItems,
        ILogger logger)
    {
        DataItems = dataItems;
        Logger = logger;

        DataItems.ItemsChanged += OnDataItemsChanged;
    }

    protected LockableObservableDataSet<TKey, TItem> DataItems { get; }
    protected int NumberOfFailedItems { get; private set; }
    protected int NumberOfSuccessfulItems { get; private set; }
    protected ILogger Logger { get; }

    protected abstract string ItemTypeName { get; }

    public virtual async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("Started loading {ItemType} data", ItemTypeName);

        ResetItemCounters();
        SetStatus(DataServiceStatus.LoadingData);

        await PrepareAsync(cancellationToken).ConfigureAwait(false);

        bool isDirty = true;

        try
        {
            await foreach (var item in GetDataAsync(cancellationToken))
            {
                if (item is null)
                {
                    ++NumberOfFailedItems;
                }
                else
                {
                    ++NumberOfSuccessfulItems;
                    await FillItemAsync(item, cancellationToken).ConfigureAwait(false);
                }
            }

            await FinishAsync(cancellationToken).ConfigureAwait(false);

            isDirty = false;

            Logger.LogInformation("Finished loading {ItemType} data", ItemTypeName);
        }
        catch (Exception ex) when (ex.IsDriveClientException())
        {
            Logger.LogWarning("Failed to load {ItemType} data: {ErrorMessage}", ItemTypeName, ex.CombinedMessage());
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Loading {ItemType} data was cancelled", ItemTypeName);
        }
        finally
        {
            var allItemsFailedToBeLoaded = NumberOfFailedItems > 0 && NumberOfSuccessfulItems == 0;

            if (allItemsFailedToBeLoaded)
            {
                ResetItemCounters();
                isDirty = true;
            }

            SetStatus(isDirty ? DataServiceStatus.Failed : DataServiceStatus.Succeeded);
        }
    }

    protected abstract IAsyncEnumerable<TItem?> GetDataAsync(CancellationToken cancellationToken);

    private async Task PrepareAsync(CancellationToken cancellationToken)
    {
        using var dataItems = await DataItems.GetItemsAsync(cancellationToken).ConfigureAwait(false);

        foreach (var item in dataItems)
        {
            _dirtyKeys.Add(item.Id);
        }
    }

    private async Task FillItemAsync(TItem item, CancellationToken cancellationToken)
    {
        using var dataItems = await DataItems.GetItemsAsync(cancellationToken).ConfigureAwait(false);
        dataItems.AddOrUpdate(item);
    }

    private async Task FinishAsync(CancellationToken cancellationToken)
    {
        using var dataItems = await DataItems.GetItemsAsync(cancellationToken).ConfigureAwait(false);

        var keysToRemove = _dirtyKeys.ToArray();
        _dirtyKeys.Clear();

        foreach (var key in keysToRemove)
        {
            dataItems.Remove(key);
        }
    }

    private void OnDataItemsChanged(object? sender, ItemsChangedEventArgs<TKey, TItem> e)
    {
        if (e.ChangeType is ItemsChangeType.Added or
            ItemsChangeType.Updated or
            ItemsChangeType.AttemptedToUpdate or
            ItemsChangeType.Removed or
            ItemsChangeType.AttemptedToRemove)
        {
            _dirtyKeys.Remove(e.Key);
        }
    }

    private void SetStatus(DataServiceStatus value)
    {
        DataItems.OnStateChanged(new DataServiceState
        {
            Status = value,
            NumberOfFailedItems = NumberOfFailedItems,
        });
    }

    private void ResetItemCounters()
    {
        NumberOfFailedItems = 0;
        NumberOfSuccessfulItems = 0;
    }
}
