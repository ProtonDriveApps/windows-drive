using Microsoft.Extensions.Logging;
using Proton.Drive.App.Drive.Services.Shared;
using Proton.Drive.Sdk.Sync.Client.Shares.SharedWithMe;

namespace Proton.Drive.App.Drive.Services.SharedWithMe;

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
