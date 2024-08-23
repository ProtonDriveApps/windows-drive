namespace ProtonDrive.App.Mapping;

internal interface ISyncFolderPathProvider
{
    public string? GetForeignDevicesFolderPath();
    public string? GetSharedWithMeItemsFolderPath();
}
