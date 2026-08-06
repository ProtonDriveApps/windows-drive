using Proton.Drive.Shared;

namespace Proton.Drive.App.Drive.Services.Shared;

public interface IDataSetProvider<TKey, TItem>
    where TKey : IEquatable<TKey>
    where TItem : IIdentifiable<TKey>
{
    event EventHandler<ItemsChangedEventArgs<TKey, TItem>> ItemsChanged;
    event EventHandler<DataServiceState> StateChanged;

    public DataServiceState State { get; }

    Task<IReadOnlyDataSet<TKey, TItem>> GetItemsAsync(CancellationToken cancellationToken);
}
