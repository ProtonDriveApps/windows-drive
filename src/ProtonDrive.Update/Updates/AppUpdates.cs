using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Diagnostics;
using ProtonDrive.Update.Config;
using ProtonDrive.Update.Files;
using ProtonDrive.Update.Files.Downloadable;
using ProtonDrive.Update.Files.Executable;
using ProtonDrive.Update.Files.UpdatesFolder;
using ProtonDrive.Update.Files.Validatable;
using ProtonDrive.Update.Releases;
using ProtonDrive.Update.Repositories;

namespace ProtonDrive.Update.Updates;

/// <summary>
/// Performs app update related operations.
/// Provides release history, checks for update, downloads, validates and starts update.
/// </summary>
internal class AppUpdates : IAppUpdates
{
    private readonly AppLaunchMode _appLaunchMode;
    private readonly IReleaseRepository _releaseStorage;
    private readonly IUpdatesFolder _updatesFolder;
    private readonly FileLocation _fileLocation;
    private readonly IDownloadableFile _downloadable;
    private readonly IValidatableFile _validatable;
    private readonly IExecutableFile _executable;

    public AppUpdates(
        ILoggerFactory loggerFactory,
        AppUpdateConfig config,
        IHttpClientFactory httpClientFactory,
        IOsProcesses osProcesses,
        AppLaunchMode appLaunchMode)
    {
        _appLaunchMode = appLaunchMode;

        var checkForUpdateHttpClient = httpClientFactory.CreateClient(config.CheckForUpdateHttpClientName);
        var downloadUpdateHttpClient = httpClientFactory.CreateClient(config.DownloadUpdateHttpClientName);

        _releaseStorage =
            new OrderedReleaseRepository(
                new SafeReleaseRepository(
                    new WebReleaseRepository(config, checkForUpdateHttpClient)));

        _updatesFolder =
            new SafeUpdatesFolder(
                new UpdatesFolder(config.UpdatesFolderPath, config.CurrentVersion));

        _fileLocation = new FileLocation(_updatesFolder.Path);

        _downloadable =
            new SafeDownloadableFile(
                new LoggingDownloadableFile(
                    loggerFactory.CreateLogger<LoggingDownloadableFile>(),
                    new DownloadableFile(downloadUpdateHttpClient)));

        _validatable =
            new SafeValidatableFile(
                new LoggingValidatableFile(
                    loggerFactory.CreateLogger<LoggingValidatableFile>(),
                    new CachingValidatableFile(
                        new ValidatableFile())));

        _executable =
            new SafeExecutableFile(
                new ExecutableFile(osProcesses));
    }

    public void Cleanup()
    {
        _updatesFolder.Cleanup();
    }

    internal async Task<IReadOnlyList<Release>> GetReleaseHistoryAsync()
    {
        var releases = await _releaseStorage.GetReleasesAsync().ConfigureAwait(false);

        return releases.ToList();
    }

    internal IReadOnlyList<Release> GetCachedReleaseHistory()
    {
        var releases = _releaseStorage.GetReleasesFromCache();

        return releases.ToList();
    }

    internal async Task DownloadAsync(Release release)
    {
        await _downloadable.DownloadAsync(release.File.Url, FilePath(release)).ConfigureAwait(false);
    }

    internal async Task<bool> ValidateAsync(Release release)
    {
        var checksum = Convert.FromHexString(release.File.Sha512Checksum);

        var localPath = FilePath(release);

        return await _validatable.IsValidAsync(localPath, checksum).ConfigureAwait(false);
    }

    internal void StartUpdating(Release release, bool forceNonSilent = false)
    {
        // Prevent infinite installation failure loop
        _releaseStorage.ClearReleasesCache();

        var useSilentMode = _appLaunchMode == AppLaunchMode.Quiet && !forceNonSilent;

        var installerPath = FilePath(release);
        var installerArguments = useSilentMode ? release.File.SilentArguments : release.File.Arguments;

        const string commandInterpreterPath = "cmd.exe";

        var commandBuilder = new StringBuilder(
            $"""
             /c "start /wait "" "{installerPath}"
             """);

        if (!string.IsNullOrEmpty(installerArguments))
        {
            commandBuilder.Append(' ');
            commandBuilder.Append(installerArguments);
        }

        var currentExecutablePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (currentExecutablePath is not null)
        {
            commandBuilder.Append($" & if errorlevel 1 \"{currentExecutablePath}\"");

            if (useSilentMode)
            {
                commandBuilder.Append(" -quiet");
            }
        }

        commandBuilder.Append('"');

        _executable.Execute(commandInterpreterPath, commandBuilder.ToString(), ProcessWindowStyle.Hidden);
    }

    internal string FilePath(Release release)
    {
        return _fileLocation.GetPath(release.File.Url);
    }
}
