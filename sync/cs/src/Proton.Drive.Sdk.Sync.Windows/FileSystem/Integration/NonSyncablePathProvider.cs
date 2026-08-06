using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Shared.Configuration;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Integration;

internal sealed class NonSyncablePathProvider : INonSyncablePathProvider
{
    private readonly Lazy<IReadOnlyList<string>> _paths;

    public NonSyncablePathProvider(AppConfig appConfig)
    {
        _paths = new Lazy<IReadOnlyList<string>>(() => Array.AsReadOnly(
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            appConfig.AppDataPath,
        ]));
    }

    public IReadOnlyList<string> Paths => _paths.Value;
}
