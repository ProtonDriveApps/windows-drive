using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Update.Files.UpdatesFolder;

/// <summary>
/// Represents directory of downloaded updates, performs cleanup of outdated downloads.
/// </summary>
internal class UpdatesFolder : IUpdatesFolder
{
    private readonly string _path;
    private readonly Version _currentVersion;

    public UpdatesFolder(string path, Version currentVersion)
    {
        Ensure.NotNullOrEmpty(path, nameof(path));

        _path = path;
        _currentVersion = currentVersion;
    }

    public string Path
    {
        get
        {
            Directory.CreateDirectory(_path);
            return _path;
        }
    }

    public void Cleanup()
    {
        DeleteSubfolders(_path);

        DeleteOldUpdates(_path, _currentVersion);
    }

    private void DeleteSubfolders(string path)
    {
        foreach (var dir in Directory.EnumerateDirectories(path))
        {
            Directory.Delete(dir, true);
        }
    }

    private static void DeleteOldUpdates(string dir, Version currentVersion)
    {
        var fallbackExpirationTime = DateTime.UtcNow - TimeSpan.FromDays(30);

        var filesToDelete = Directory.EnumerateFiles(dir).Where(file => !IsVersionCache(file) && IsOutdated(file, currentVersion, fallbackExpirationTime));

        foreach (var file in filesToDelete)
        {
            /* Deleting installer of the current app version might fail on first
             run after the installation if the installer has not yet exited. */
            File.Delete(file);
        }
    }

    private static bool IsOutdated(string path, Version currentVersion, DateTime fallbackExpirationTime)
    {
        if (!TryGetFileVersion(path, out var fileVersion))
        {
            return new FileInfo(path).LastWriteTimeUtc <= fallbackExpirationTime;
        }

        return fileVersion <= currentVersion;
    }

    private static bool IsVersionCache(string path)
    {
        return string.Equals(System.IO.Path.GetFileName(path), "version.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetFileVersion(string path, [MaybeNullWhen(false)] out Version version)
    {
        var info = FileVersionInfo.GetVersionInfo(path);
        if (!Version.TryParse(info.FileVersion, out var nonNormalizedVersion))
        {
            version = null;
            return false;
        }

        version = nonNormalizedVersion.ToNormalized();
        return true;
    }
}
