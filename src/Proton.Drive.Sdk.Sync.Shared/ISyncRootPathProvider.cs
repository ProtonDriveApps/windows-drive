namespace Proton.Drive.Sdk.Sync.Shared;

public interface ISyncRootPathProvider
{
    IReadOnlyList<string> GetOfTypes(IReadOnlyCollection<MappingType> types);
}
