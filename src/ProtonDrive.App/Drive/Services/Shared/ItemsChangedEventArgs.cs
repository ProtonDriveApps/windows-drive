using System;

namespace ProtonDrive.App.Drive.Services.Shared;

public class ItemsChangedEventArgs<TKey, TItem> : EventArgs
{
    private readonly TKey? _key;
    private readonly TItem? _item;

    internal ItemsChangedEventArgs(ItemsChangeType changeType, TKey? key, TItem? item)
    {
        ChangeType = changeType;
        _key = key;
        _item = item;
    }

    public ItemsChangeType ChangeType { get; }

    public TKey Key => _key ?? throw new InvalidOperationException($"{nameof(Key)} is not set");

    public TItem Item => _item ?? throw new InvalidOperationException($"{nameof(Item)} is not set");
}
