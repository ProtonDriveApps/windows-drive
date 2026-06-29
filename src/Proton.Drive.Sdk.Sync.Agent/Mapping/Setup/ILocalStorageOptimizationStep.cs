using Proton.Drive.Sdk.Sync.Agent.Settings;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping.Setup;

internal interface ILocalStorageOptimizationStep
{
    StorageOptimizationErrorCode? Execute(RemoteToLocalMapping mapping);
}
