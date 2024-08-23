namespace ProtonDrive.App.Mapping.Teardown;

internal interface ILocalSpecialSubfoldersDeletionStep
{
    void DeleteSpecialSubfolders(string? rootPath);
}
