using System.IO;
using ProtonDrive.Shared.IO;

namespace ProtonDrive.Sync.Adapter.UpdateDetection;

internal interface IItemExclusionFilter
{
    public bool ShouldBeIgnored(string name, FileAttributes attributes, PlaceholderState placeholderState, bool parentIsSyncRoot);
}
