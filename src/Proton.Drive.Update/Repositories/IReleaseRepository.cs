using Proton.Drive.Update.Releases;

namespace Proton.Drive.Update.Repositories;

internal interface IReleaseRepository
{
    Task<IEnumerable<Release>> GetReleasesAsync();

    IEnumerable<Release> GetReleasesFromCache();

    void ClearReleasesCache();
}
