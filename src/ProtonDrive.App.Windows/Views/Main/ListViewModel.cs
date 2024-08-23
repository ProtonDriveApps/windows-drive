using System;
using CommunityToolkit.Mvvm.ComponentModel;
using ProtonDrive.App.Windows.Extensions;
using ProtonDrive.Shared;

namespace ProtonDrive.App.Windows.Views.Main;

internal abstract class ListViewModel<T, TId> : ObservableObject
    where T : IIdentifiable<TId>
    where TId : IEquatable<TId>
{
    private T? _selectedItem;

    public ObservableCollectionOfIdentifiableItems<T, TId> Items { get; } = [];

    public T? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                OnSelectedItemChanged(_selectedItem);
            }
        }
    }

    protected virtual void OnSelectedItemChanged(T? item)
    {
    }
}
