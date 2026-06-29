namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.OnDemand;

public interface IPlaceholderToRegularItemConverter
{
    bool TryConvertToRegularFolder(string path, bool skipRoot);
    bool TryConvertToRegularFile(string path);
}
