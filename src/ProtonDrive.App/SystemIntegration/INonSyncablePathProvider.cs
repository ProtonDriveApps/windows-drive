using System.Collections.Generic;

namespace ProtonDrive.App.SystemIntegration;

public interface INonSyncablePathProvider
{
    IReadOnlyList<string> Paths { get; }
}
