using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Agent.Account;
using Proton.Drive.Sdk.Sync.Agent.Services;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Logging;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Drive.Services.Shared;

internal abstract class DataServiceBase<TKey, TItem> : IAccountStateAware, IStoppableService
    where TKey : IEquatable<TKey>
    where TItem : IIdentifiable<TKey>
{
    private readonly LockableObservableDataSet<TKey, TItem> _dataItems;
    private readonly StateBasedUpdateDetectorBase<TKey, TItem> _stateBasedUpdateDetector;

    private bool _stopping;

    protected DataServiceBase(
        LockableObservableDataSet<TKey, TItem> dataItems,
        StateBasedUpdateDetectorBase<TKey, TItem> stateBasedUpdateDetector,
        ILogger logger)
    {
        _dataItems = dataItems;
        _stateBasedUpdateDetector = stateBasedUpdateDetector;

        UpdateDetection = logger.GetSingleActionWithExceptionsLoggingAndCancellationHandling(
            DetectUpdatesAsync,
            nameof(DataServiceBase<TKey, TItem>));
    }

    protected SingleAction UpdateDetection { get; }

    public void Refresh()
    {
        UpdateDetection.RunAsync();
    }

    void IAccountStateAware.OnAccountStateChanged(AccountState value)
    {
        if (value.Status is not AccountStatus.Succeeded)
        {
            UpdateDetection.Cancel();
        }

        if (value.Status is AccountStatus.None)
        {
            _ = ClearItemsAsync(CancellationToken.None);
        }
    }

    Task IStoppableService.StopAsync(CancellationToken cancellationToken)
    {
        _stopping = true;
        UpdateDetection.Cancel();

        return UpdateDetection.WaitForCompletionAsync();
    }

    private async Task DetectUpdatesAsync(CancellationToken cancellationToken)
    {
        if (_stopping)
        {
            return;
        }

        await _stateBasedUpdateDetector.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task ClearItemsAsync(CancellationToken cancellationToken)
    {
        return _dataItems.ClearAsync(cancellationToken);
    }
}
