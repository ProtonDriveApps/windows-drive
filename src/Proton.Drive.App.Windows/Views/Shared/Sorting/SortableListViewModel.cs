using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Proton.Drive.App.Windows.Views.Shared.Sorting;

internal abstract class SortableListViewModel : ObservableObject
{
    protected SortableListViewModel()
    {
        SortCommand = new RelayCommand<GridColumnViewModel>(SortItems);
    }

    public ICommand SortCommand { get; }

    protected abstract void SortItems(GridColumnViewModel? column);
}
