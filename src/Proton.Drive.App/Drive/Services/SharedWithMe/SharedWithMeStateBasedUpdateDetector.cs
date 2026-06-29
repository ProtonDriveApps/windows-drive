using Microsoft.Extensions.Logging;
using Proton.Drive.App.Drive.Services.Shared;
using Proton.Drive.Sdk.Sync.Client.Shares.SharedWithMe;

namespace Proton.Drive.App.Drive.Services.SharedWithMe;

internal sealed class SharedWithMeStateBasedUpdateDetector : StateBasedUpdateDetectorBase<string, SharedWithMeItem>
{
    private readonly ISharedWithMeClient _dataClient;

    public SharedWithMeStateBasedUpdateDetector(
        SharedWithMeDataItems dataItems,
        ISharedWithMeClient dataClient,
        ILogger<SharedWithMeStateBasedUpdateDetector> logger)
        : base(dataItems, logger)
    {
        _dataClient = dataClient;
    }

    protected override string ItemTypeName => "Shared with me";

    protected override IAsyncEnumerable<SharedWithMeItem?> GetDataAsync(CancellationToken cancellationToken)
    {
        return _dataClient.GetSharedWithMeItemsAsync(cancellationToken);
    }
}
