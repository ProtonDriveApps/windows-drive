using System;
using System.Diagnostics;
using ProtonDrive.Update.Helpers;

namespace ProtonDrive.Update.Files.Executable;

/// <summary>
/// Wraps expected exceptions of <see cref="ExecutableFile"/> into <see cref="AppUpdateException"/>.
/// </summary>
internal class SafeExecutableFile : IExecutableFile
{
    private readonly IExecutableFile _origin;

    public SafeExecutableFile(IExecutableFile origin)
    {
        _origin = origin;
    }

    public void Execute(string filename, string? arguments, ProcessWindowStyle windowStyle = ProcessWindowStyle.Normal)
    {
        try
        {
            _origin.Execute(filename, arguments, windowStyle);
        }
        catch (Exception e) when (e.IsProcessException())
        {
            throw new AppUpdateException("Failed to start the update installer", e);
        }
    }
}
