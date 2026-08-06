using Proton.Drive.Shared;

namespace Proton.Drive.App.Drive.Services.Shared;

public interface IDataSet<TKey, TItem> : IReadOnlyDataSet<TKey, TItem>
    where TKey : IEquatable<TKey>
    where TItem : IIdentifiable<TKey>
{
    public void AddOrUpdate(TItem item);

    public void Remove(TKey id);

    public void Clear();
}
