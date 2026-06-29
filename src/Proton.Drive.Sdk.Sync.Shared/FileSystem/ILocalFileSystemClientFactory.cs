namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public interface ILocalFileSystemClientFactory
{
    IFileSystemClient<long> CreateClassicClient();
    IFileSystemClient<long> CreateOnDemandHydrationClient();
    IPhotoFileSystemClient<long> CreatePhotoClient();
}
