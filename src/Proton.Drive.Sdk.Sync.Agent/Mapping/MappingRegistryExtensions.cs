namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

public static class MappingRegistryExtensions
{
    public static async Task ClearAsync(this IMappingRegistry mappingRegistry, CancellationToken cancellationToken)
    {
        using var mappings = await mappingRegistry.GetMappingsAsync(cancellationToken).ConfigureAwait(false);

        mappings.Clear();
    }
}
