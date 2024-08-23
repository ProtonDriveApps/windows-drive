namespace ProtonDrive.Update.Files.UpdatesFolder;

internal interface IUpdatesFolder
{
    string Path { get; }

    void Cleanup();
}
