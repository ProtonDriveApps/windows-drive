using System.Collections.Generic;

namespace ProtonDrive.Update.Contracts;

internal class ReleasesContract
{
    public IReadOnlyList<ReleaseContract> Releases { get; set; } = new List<ReleaseContract>();

    // To support legacy content
    public IReadOnlyList<CategoryContract> Categories { get; set; } = new List<CategoryContract>();
}
