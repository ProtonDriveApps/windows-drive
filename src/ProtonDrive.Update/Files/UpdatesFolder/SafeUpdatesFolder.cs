using System;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Update.Files.UpdatesFolder;

/// <summary>
/// Wraps expected exceptions of <see cref="UpdatesFolder"/> into <see cref="AppUpdateException"/>.
/// </summary>
internal class SafeUpdatesFolder : IUpdatesFolder
{
    private readonly IUpdatesFolder _origin;

    public SafeUpdatesFolder(IUpdatesFolder origin)
    {
        _origin = origin;
    }

    public string Path
    {
        get
        {
            try
            {
                return _origin.Path;
            }
            catch (Exception e) when (e.IsFileAccessException())
            {
                throw new AppUpdateException("Failed to create updates download folder", e);
            }
        }
    }

    public void Cleanup()
    {
        try
        {
            _origin.Cleanup();
        }
        catch (Exception e) when (e.IsFileAccessException())
        {
            throw new AppUpdateException("Failed to cleanup downloaded app updates", e);
        }
    }
}
