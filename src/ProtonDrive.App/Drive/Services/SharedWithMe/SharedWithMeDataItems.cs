using System;
using ProtonDrive.App.Drive.Services.Shared;
using ProtonDrive.Client.Shares.SharedWithMe;

namespace ProtonDrive.App.Drive.Services.SharedWithMe;

internal class SharedWithMeDataItems : LockableObservableDataSet<string, SharedWithMeItem>, ISharedWithMeDataProvider
{
    public event EventHandler? RefreshRequested;

    public void RequestRefresh()
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }
}
