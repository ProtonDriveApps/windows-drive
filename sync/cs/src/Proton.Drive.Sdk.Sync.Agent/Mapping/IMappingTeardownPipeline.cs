using Proton.Drive.Sdk.Sync.Agent.Settings;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

internal interface IMappingTeardownPipeline
{
    Task<MappingState> TearDownAsync(RemoteToLocalMapping mapping, CancellationToken cancellationToken);
}
