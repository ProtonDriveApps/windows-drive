using Proton.Drive.Update.Releases;

namespace Proton.Drive.Update.Repositories;

/// <summary>
/// Orders stream of app releases by version number in descending order.
/// </summary>
internal class OrderedReleaseRepository : IReleaseRepository
{
    private readonly IReleaseRepository _origin;

    public OrderedReleaseRepository(IReleaseRepository origin)
    {
        _origin = origin;
    }

    public async Task<IEnumerable<Release>> GetReleasesAsync()
    {
        return (await _origin.GetReleasesAsync().ConfigureAwait(false))
            .OrderByDescending(r => r);
    }

    public IEnumerable<Release> GetReleasesFromCache()
    {
        return _origin.GetReleasesFromCache()
            .OrderByDescending(r => r);
    }

    public void ClearReleasesCache()
    {
        _origin.ClearReleasesCache();
    }
}
