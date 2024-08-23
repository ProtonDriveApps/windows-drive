using ProtonDrive.App.Drive.Services.Shared;
using ProtonDrive.Client.Shares.SharedWithMe;

namespace ProtonDrive.App.Drive.Services.SharedWithMe;

public interface ISharedWithMeDataProvider : IDataSetProvider<string, SharedWithMeItem>
{
    void RequestRefresh();
}
