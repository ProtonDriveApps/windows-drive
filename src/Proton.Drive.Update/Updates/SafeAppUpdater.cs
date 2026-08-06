namespace Proton.Drive.Update.Updates;

/// <summary>
/// Suppresses expected exceptions of <see cref="AppUpdater"/>.
/// </summary>
internal class SafeAppUpdater : IAppUpdateCleanup
{
    private readonly IAppUpdateCleanup _origin;

    public SafeAppUpdater(IAppUpdateCleanup origin)
    {
        _origin = origin;
    }

    public void Cleanup()
    {
        try
        {
            _origin.Cleanup();
        }
        catch (AppUpdateException)
        {
            // Suppress expected exceptions
        }
    }
}
