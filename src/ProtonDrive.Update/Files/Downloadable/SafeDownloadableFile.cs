using System;
using System.Threading.Tasks;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Update.Helpers;

namespace ProtonDrive.Update.Files.Downloadable;

/// <summary>
/// Wraps expected exceptions of <see cref="DownloadableFile"/> into <see cref="AppUpdateException"/>.
/// </summary>
internal class SafeDownloadableFile : IDownloadableFile
{
    private readonly IDownloadableFile _origin;

    public SafeDownloadableFile(IDownloadableFile origin)
    {
        _origin = origin;
    }

    public async Task DownloadAsync(string url, string filename)
    {
        try
        {
            await _origin.DownloadAsync(url, filename).ConfigureAwait(false);
        }
        catch (Exception e) when (e.IsCommunicationException() || e.IsFileAccessException())
        {
            throw new AppUpdateException("Failed to download an update", e);
        }
    }
}
