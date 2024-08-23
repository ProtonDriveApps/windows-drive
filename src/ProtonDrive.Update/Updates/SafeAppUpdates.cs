namespace ProtonDrive.Update.Updates;

/// <summary>
/// Suppresses expected exceptions of <see cref="AppUpdates"/>.
/// </summary>
internal class SafeAppUpdates : IAppUpdates
{
    private readonly IAppUpdates _origin;

    public SafeAppUpdates(IAppUpdates origin)
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
