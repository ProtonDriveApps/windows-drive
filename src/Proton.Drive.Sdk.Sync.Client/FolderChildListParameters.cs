using Refit;

namespace Proton.Drive.Sdk.Sync.Client;

public sealed class FolderChildListParameters
{
    [AliasAs("Page")]
    public int? PageIndex { get; set; }
    public int? PageSize { get; set; }
    public bool ShowAll { get; set; }
}
