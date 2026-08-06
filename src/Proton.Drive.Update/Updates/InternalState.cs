using Proton.Drive.Update.Releases;

namespace Proton.Drive.Update.Updates;

/// <summary>
/// Internal cloneable app update state.
/// </summary>
internal class InternalState
{
    public InternalState(double rolloutEligibilityThreshold, AppUpdater appUpdater, IReadOnlyList<Release> releases, Release newRelease)
    {
        RolloutEligibilityThreshold = rolloutEligibilityThreshold;
        AppUpdater = appUpdater;
        Releases = releases;
        NewRelease = newRelease;
    }

    public double RolloutEligibilityThreshold { get; }
    public AppUpdater AppUpdater { get; set; }
    public bool EarlyAccess { get; set; }
    public IReadOnlyList<Release> Releases { get; set; }
    public Release NewRelease { get; set; }
    public bool Ready { get; set; }

    public InternalState Clone()
    {
        return (InternalState)MemberwiseClone();
    }
}
