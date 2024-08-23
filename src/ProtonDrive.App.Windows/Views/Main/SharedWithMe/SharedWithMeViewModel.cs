namespace ProtonDrive.App.Windows.Views.Main.SharedWithMe;

internal sealed class SharedWithMeViewModel : PageViewModel
{
    public SharedWithMeViewModel(SharedWithMeListViewModel sharedWithMeList)
    {
        SharedWithMeList = sharedWithMeList;
    }

    public SharedWithMeListViewModel SharedWithMeList { get; }

    internal override async void OnActivated()
    {
        base.OnActivated();

        await SharedWithMeList.LoadDataAsync().ConfigureAwait(true);
    }
}
