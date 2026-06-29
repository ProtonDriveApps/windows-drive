namespace Proton.Drive.Update.Updates;

/// <summary>
/// Performs asynchronous app updates download folder cleanup.
/// </summary>
internal class AsyncCleanableAppUpdater : IAppUpdateCleanup
{
    private readonly IAppUpdateCleanup _origin;

    public AsyncCleanableAppUpdater(IAppUpdateCleanup origin)
    {
        _origin = origin;
    }

    public void Cleanup()
    {
        Task.Run(() => _origin.Cleanup());
    }
}
