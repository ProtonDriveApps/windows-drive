namespace ProtonDrive.App.Windows.SystemIntegration;

internal interface IFileSystemItemTypeProvider
{
    string? GetFileType(string filename);
    string? GetFolderType();
}
