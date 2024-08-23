using System;
using System.Collections.Generic;
using ProtonDrive.Shared;

namespace ProtonDrive.App.Drive.Services.Shared;

public interface IReadOnlyDataSet<out TKey, out TItem> : IReadOnlyCollection<TItem>, IDisposable
    where TKey : IEquatable<TKey>
    where TItem : IIdentifiable<TKey>;
