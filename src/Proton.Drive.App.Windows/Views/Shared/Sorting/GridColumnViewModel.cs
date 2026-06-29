using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Proton.Drive.App.Windows.Views.Shared.Sorting;

internal sealed class GridColumnViewModel : ObservableObject
{
    private ListSortDirection? _sortDirection;

    public GridColumnViewModel(string name, string displayName, string? tooltip = default)
    {
        Name = name;
        DisplayName = displayName;
        Tooltip = tooltip;
    }

    public string Name { get; }

    public string DisplayName { get; }

    public string? Tooltip { get; }

    public ListSortDirection? SortDirection
    {
        get => _sortDirection;
        set => SetProperty(ref _sortDirection, value);
    }

    public void ClearSorting()
    {
        SortDirection = default;
    }
}
