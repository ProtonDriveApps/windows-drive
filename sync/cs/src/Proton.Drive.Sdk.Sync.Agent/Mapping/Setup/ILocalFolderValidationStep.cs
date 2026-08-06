using Proton.Drive.Sdk.Sync.Agent.Settings;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup;

internal interface ILocalFolderValidationStep
{
    Task<MappingErrorCode> ValidateAsync(RemoteToLocalMapping mapping, IReadOnlySet<string> otherLocalSyncFolders, CancellationToken cancellationToken);
}
