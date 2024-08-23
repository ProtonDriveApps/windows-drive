using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ProtonDrive.Update.Helpers;
using ProtonDrive.Update.Releases;

namespace ProtonDrive.Update.Repositories;

/// <summary>
/// Wraps expected exceptions of <see cref="WebReleaseRepository"/> into <see cref="AppUpdateException"/>.
/// </summary>
internal class SafeReleaseRepository : IReleaseRepository
{
    private readonly IReleaseRepository _storage;

    public SafeReleaseRepository(IReleaseRepository storage)
    {
        _storage = storage;
    }

    public async Task<IEnumerable<Release>> GetReleasesAsync()
    {
        try
        {
            return await _storage.GetReleasesAsync().ConfigureAwait(false);
        }
        catch (JsonException e)
        {
            throw new AppUpdateException("Release history has unsupported format", e);
        }
        catch (Exception e) when (e.IsCommunicationException())
        {
            throw new AppUpdateException("Failed to download release history", e);
        }
    }

    public IEnumerable<Release> GetReleasesFromCache()
    {
        try
        {
            return _storage.GetReleasesFromCache();
        }
        catch (JsonException e)
        {
            throw new AppUpdateException("Release history has unsupported format", e);
        }
    }

    public void ClearReleasesCache()
    {
        _storage.ClearReleasesCache();
    }
}
