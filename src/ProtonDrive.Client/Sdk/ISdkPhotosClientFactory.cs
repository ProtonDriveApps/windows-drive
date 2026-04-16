using Proton.Drive.Sdk;

namespace ProtonDrive.Client.Sdk;

public interface ISdkPhotosClientFactory
{
    ProtonPhotosClient GetOrCreatePhotosClient();
}
