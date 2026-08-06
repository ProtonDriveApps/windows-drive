namespace Proton.Drive.Update.Updates;

/// <summary>
/// Performs app updates download directory cleanup only once.
/// </summary>
internal class CleanableOnceAppUpdater : IAppUpdateCleanup
{
    private readonly IAppUpdateCleanup _origin;
    private int _cleaned;

    public CleanableOnceAppUpdater(IAppUpdateCleanup origin)
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
