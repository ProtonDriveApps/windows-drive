using System;
using System.Collections.Generic;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Shared.Configuration;

namespace ProtonDrive.App.Windows.SystemIntegration;

internal sealed class NonSyncablePathProvider : INonSyncablePathProvider
{
    private readonly Lazy<IReadOnlyList<string>> _paths;

    public NonSyncablePathProvider(AppConfig appConfig)
    {
        _paths = new Lazy<IReadOnlyList<string>>(() => Array.AsReadOnly(new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            appConfig.AppDataPath,
        }));
    }

    public IReadOnlyList<string> Paths => _paths.Value;
}
