namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public enum ShareTargetType
{
    Root = 0,
    Folder = 1,
    File = 2,
    Album = 3,
    Photo = 4,

    /// <summary>
    /// Proton Doc, Sheet, etc.
    /// </summary>
    ProtonVendor = 5,
}
