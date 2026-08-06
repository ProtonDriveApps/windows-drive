using Proton.Drive.App.Drive.Services.Shared;
using Proton.Drive.Sdk.Sync.Client.Shares.SharedWithMe;

namespace Proton.Drive.App.Drive.Services.SharedWithMe;

public interface ISharedWithMeDataProvider : IDataSetProvider<string, SharedWithMeItem>
{
    void RequestRefresh();
}
