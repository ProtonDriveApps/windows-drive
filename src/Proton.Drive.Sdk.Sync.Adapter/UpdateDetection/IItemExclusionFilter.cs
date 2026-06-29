using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;

internal interface IItemExclusionFilter
{
    public bool ShouldBeIgnored(string name, FileAttributes attributes, PlaceholderState placeholderState, bool parentIsSyncRoot);
}
