using Microsoft.Extensions.Logging;
using ProtonDrive.App.Drive.Services.Shared;
using ProtonDrive.Client.Shares.SharedWithMe;

namespace ProtonDrive.App.Drive.Services.SharedWithMe;

internal class SharedWithMeDataService : DataServiceBase<string, SharedWithMeItem>
{
    public SharedWithMeDataService(
        SharedWithMeDataItems dataItems,
        SharedWithMeStateBasedUpdateDetector stateBasedUpdateDetection,
        ILogger<SharedWithMeDataService> logger)
        : base(
            dataItems,
            stateBasedUpdateDetection,
            logger)
    {
        dataItems.RefreshRequested += (_, _) => Refresh();
    }
}
