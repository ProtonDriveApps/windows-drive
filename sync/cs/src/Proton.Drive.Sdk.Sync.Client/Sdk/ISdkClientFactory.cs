using Proton.Drive.Sdk;

namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal interface ISdkClientFactory
{
    public ProtonDriveClient GetOrCreateClient();
}
