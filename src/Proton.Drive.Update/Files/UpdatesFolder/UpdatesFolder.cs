using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Update.Files.UpdatesFolder;

/// <summary>
/// Represents directory of downloaded updates, performs cleanup of outdated downloads.
/// </summary>
internal class UpdatesFolder : IUpdatesFolder
{
    private readonly string _path;
    private readonly Version _currentVersion;
    private readonly ILogger<UpdatesFolder> _logger;

    public UpdatesFolder(string path, Version currentVersion, ILogger<UpdatesFolder> logger)
    {
        Ensure.NotNullOrEmpty(path, nameof(path));

        _path = path;
        _currentVersion = currentVersion;
        _logger = logger;
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
            _logger.LogInformation("Deleting app update subfolder \"{Name}\"", System.IO.Path.GetFileName(dir));

            Directory.Delete(dir, true);
        }
    }

    private void DeleteOldUpdates(string dir, Version currentVersion)
    {
        var fallbackExpirationTime = DateTime.UtcNow - TimeSpan.FromDays(30);

        var filesToDelete = Directory.EnumerateFiles(dir).Where(file => !IsVersionCache(file) && IsOutdated(file, currentVersion, fallbackExpirationTime));

        foreach (var file in filesToDelete)
        {
            _logger.LogInformation("Deleting app update file \"{Name}\"", System.IO.Path.GetFileName(file));

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
