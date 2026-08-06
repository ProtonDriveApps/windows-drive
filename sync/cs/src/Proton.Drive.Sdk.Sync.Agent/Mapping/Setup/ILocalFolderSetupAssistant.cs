using Proton.Drive.Sdk.Sync.Agent.Settings;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup;

internal interface ILocalFolderSetupAssistant
{
    MappingErrorCode? SetUpLocalFolder(RemoteToLocalMapping mapping, CancellationToken cancellationToken);
}
