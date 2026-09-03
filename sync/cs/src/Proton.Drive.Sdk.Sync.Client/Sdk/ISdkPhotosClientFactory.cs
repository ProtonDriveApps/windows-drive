namespace Proton.Drive.Sdk.Sync.Client.Sdk;

public interface ISdkPhotosClientFactory
{
    ProtonPhotosClient GetOrCreatePhotosClient();
}
