using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping;

internal interface IMappingSetupPipeline
{
    Task<MappingState> SetUpAsync(
        RemoteToLocalMapping mapping,
        IReadOnlySet<string> otherLocalSyncFolders,
        CancellationToken cancellationToken);
}
