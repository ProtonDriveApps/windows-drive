using Refit;

namespace ProtonDrive.Client;

public sealed class FolderChildListParameters
{
    [AliasAs("Page")]
    public int? PageIndex { get; set; }
    public int? PageSize { get; set; }
    public bool ShowAll { get; set; }
}
