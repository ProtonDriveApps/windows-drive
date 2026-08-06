namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;

public interface INonSyncablePathProvider
{
    IReadOnlyList<string> Paths { get; }
}
