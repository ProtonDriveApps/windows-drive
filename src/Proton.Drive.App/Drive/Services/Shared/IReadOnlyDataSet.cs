using Proton.Drive.Shared;

namespace Proton.Drive.App.Drive.Services.Shared;

public interface IReadOnlyDataSet<out TKey, out TItem> : IReadOnlyCollection<TItem>, IDisposable
    where TKey : IEquatable<TKey>
    where TItem : IIdentifiable<TKey>;
