namespace ProtonDrive.Sync.Shared.FileSystem.Integration;

public interface INonSyncablePathProvider
{
    IReadOnlyList<string> Paths { get; }
}
