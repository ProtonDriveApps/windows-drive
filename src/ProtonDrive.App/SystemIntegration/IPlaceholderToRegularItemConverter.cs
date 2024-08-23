namespace ProtonDrive.App.SystemIntegration;

public interface IPlaceholderToRegularItemConverter
{
    bool TryConvertToRegularFolder(string path);
    bool TryConvertToRegularFile(string path);
}
