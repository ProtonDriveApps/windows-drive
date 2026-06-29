namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Teardown;

internal interface ILocalSpecialSubfoldersDeletionStep
{
    void DeleteSpecialSubfolders(string? rootPath);
}
