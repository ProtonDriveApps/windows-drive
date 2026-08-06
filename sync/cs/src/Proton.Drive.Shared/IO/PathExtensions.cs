namespace Proton.Drive.Shared.IO;

public static class PathExtensions
{
    public static readonly string[] DosDeviceNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³",
    ];

    public static ReadOnlySpan<char> GetDisplayNameWithoutAccess(ReadOnlySpan<char> path)
    {
        if (path.IsEmpty)
        {
            return ReadOnlySpan<char>.Empty;
        }

        var displayName = Path.GetFileName(path);

        if (!displayName.IsEmpty)
        {
            return displayName;
        }

        // The path is the root of the volume
        var rootName = Path.GetPathRoot(path);

        // Stripping the ending path separator from the drive letter ("X:\")
        return Path.EndsInDirectorySeparator(rootName) ? rootName[..^1] : rootName;
    }
}
