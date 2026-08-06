using Proton.Drive.App.Drive.Services.Shared;
using Proton.Drive.Sdk.Sync.Client.Shares.SharedWithMe;

namespace Proton.Drive.App.Drive.Services.SharedWithMe;

internal class SharedWithMeDataItems : LockableObservableDataSet<string, SharedWithMeItem>, ISharedWithMeDataProvider
{
    public event EventHandler? RefreshRequested;

    public void RequestRefresh()
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }
}
