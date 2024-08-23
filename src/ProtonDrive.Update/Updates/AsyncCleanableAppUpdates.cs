using System.Threading.Tasks;

namespace ProtonDrive.Update.Updates;

/// <summary>
/// Performs asynchronous app updates download folder cleanup.
/// </summary>
internal class AsyncCleanableAppUpdates : IAppUpdates
{
    private readonly IAppUpdates _origin;

    public AsyncCleanableAppUpdates(IAppUpdates origin)
    {
        _origin = origin;
    }

    public void Cleanup()
    {
        Task.Run(() => _origin.Cleanup());
    }
}
