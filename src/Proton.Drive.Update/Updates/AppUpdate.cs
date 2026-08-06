using Proton.Drive.Update.Releases;

namespace Proton.Drive.Update.Updates;

/// <summary>
/// Represents app update state and an interface to update related operations.
/// Operations are performed by <see cref="AppUpdater"/>.
/// </summary>
internal class AppUpdate : IAppUpdate
{
    private readonly InternalState _state;

    public AppUpdate(double rolloutEligibilityThreshold, AppUpdater appUpdater)
        : this(new InternalState(rolloutEligibilityThreshold, appUpdater, [], Release.EmptyRelease()))
    {
    }

    private AppUpdate(InternalState state)
    {
        _state = state;
    }

    public bool IsAvailable => !_state.NewRelease.IsEmpty();

    public bool IsReady => _state.Ready;

    public string FilePath => _state.NewRelease.IsNew ? _state.AppUpdater.FilePath(_state.NewRelease) : string.Empty;

    public string? FileArguments => _state.NewRelease.IsNew ? _state.NewRelease.File.Arguments : null;

    public IReadOnlyList<IRelease> ReleaseHistory()
    {
        return _state.EarlyAccess ? GetAllReleases() : GetStableReleases();
    }

    public async Task<IAppUpdate> GetLatestAsync(bool earlyAccess, bool manual = false)
    {
        var releases = await _state.AppUpdater.GetReleaseHistoryAsync().ConfigureAwait(false);

        return WithReleases(releases, earlyAccess, manual);
    }

    public IAppUpdate GetCachedLatest(bool earlyAccess, bool manual)
    {
        var releases = _state.AppUpdater.GetCachedReleaseHistory();

        return WithReleases(releases, earlyAccess, manual);
    }

    public async Task<IAppUpdate> DownloadAsync()
    {
        if (IsAvailable && !IsReady)
        {
            await _state.AppUpdater.DownloadAsync(_state.NewRelease).ConfigureAwait(false);
        }

        return this;
    }

    public async Task<IAppUpdate> ValidateAsync()
    {
        if (!IsAvailable)
        {
            throw new AppUpdateException("There is no new release to validate");
        }

        var valid = await _state.AppUpdater.ValidateAsync(_state.NewRelease).ConfigureAwait(false);

        return WithReady(valid);
    }

    public IAppUpdate StartUpdating(bool auto)
    {
        if (!IsReady)
        {
            throw new AppUpdateException("There is no new release ready to update to");
        }

        if (auto && _state.NewRelease.IsAutoUpdateDisabled)
        {
            throw new AppUpdateException($"Automatic update to release {_state.NewRelease.Version} is disabled");
        }

        _state.AppUpdater.StartUpdating(_state.NewRelease, forceNonSilent: !auto);

        return this;
    }

    private AppUpdate WithReleases(IReadOnlyList<Release> releases, bool earlyAccess, bool manual)
    {
        var newRelease = GetNewRelease(releases, earlyAccess, manual);
        var releaseChanged = !Equals(newRelease, _state.NewRelease);

        var state = _state.Clone();
        state.EarlyAccess = earlyAccess;
        state.Releases = releases;
        state.NewRelease = newRelease;
        if (releaseChanged)
        {
            state.Ready = false;
        }

        return new AppUpdate(state);
    }

    private AppUpdate WithReady(bool ready)
    {
        var state = _state.Clone();
        state.Ready = ready;

        return new AppUpdate(state);
    }

    private IReadOnlyList<IRelease> GetAllReleases()
    {
        return _state.Releases.ToList();
    }

    private IReadOnlyList<IRelease> GetStableReleases()
    {
        return _state.Releases
            .SkipWhile(r => r.IsEarlyAccess && r.IsNew)
            .ToList();
    }

    private Release GetNewRelease(IEnumerable<Release> releases, bool earlyAccess, bool manual)
    {
        return releases
                   .FirstOrDefault(r => r.IsNew && (!r.IsEarlyAccess || earlyAccess) && (manual || r.RolloutRatio is null || r.RolloutRatio >= _state.RolloutEligibilityThreshold))
               ?? Release.EmptyRelease();
    }

    private bool Equals(Release one, Release other)
    {
        return one.Version.Equals(other.Version) &&
               one.File.Equals(other.File);
    }
}
