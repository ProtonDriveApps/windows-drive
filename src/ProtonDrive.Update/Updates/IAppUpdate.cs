using System.Threading.Tasks;

namespace ProtonDrive.Update.Updates;

/// <summary>
/// Interface to the app update state plus the app update related operations.
/// </summary>
internal interface IAppUpdate : IBaseAppUpdateState
{
    Task<IAppUpdate> GetLatestAsync(bool earlyAccess, bool manual = false);

    IAppUpdate GetCachedLatest(bool earlyAccess, bool manual = false);

    Task<IAppUpdate> DownloadAsync();

    Task<IAppUpdate> ValidateAsync();

    IAppUpdate StartUpdating(bool auto);
}
