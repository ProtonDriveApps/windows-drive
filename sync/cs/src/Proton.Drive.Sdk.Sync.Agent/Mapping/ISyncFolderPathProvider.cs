namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

internal interface ISyncFolderPathProvider
{
    public string? GetForeignDevicesFolderPath();
    public string? GetSharedWithMeRootFolderPath();
}
