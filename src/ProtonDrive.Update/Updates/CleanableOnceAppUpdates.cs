using System.Threading;

namespace ProtonDrive.Update.Updates;

/// <summary>
/// Performs app updates download directory cleanup only once.
/// </summary>
internal class CleanableOnceAppUpdates : IAppUpdates
{
    private readonly IAppUpdates _origin;
    private int _cleaned;

    public CleanableOnceAppUpdates(IAppUpdates origin)
    {
        _origin = origin;
    }

    public void Cleanup()
    {
        if (Interlocked.Exchange(ref _cleaned, 1) != 0)
        {
            return;
        }

        _origin.Cleanup();
    }
}
