using System.Collections.Generic;

namespace ProtonDrive.Update.Contracts;

internal class CategoryContract
{
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<ReleaseContract> Releases { get; set; } = new List<ReleaseContract>();
}
