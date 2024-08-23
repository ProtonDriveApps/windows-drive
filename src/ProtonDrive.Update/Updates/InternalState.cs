using System.Collections.Generic;
using ProtonDrive.Update.Releases;

namespace ProtonDrive.Update.Updates;

/// <summary>
/// Internal cloneable app update state.
/// </summary>
internal class InternalState
{
    public InternalState(double rolloutEligibilityThreshold, AppUpdates appUpdates, IReadOnlyList<Release> releases, Release newRelease)
    {
        RolloutEligibilityThreshold = rolloutEligibilityThreshold;
        AppUpdates = appUpdates;
        Releases = releases;
        NewRelease = newRelease;
    }

    public double RolloutEligibilityThreshold { get; }
    public AppUpdates AppUpdates { get; set; }
    public bool EarlyAccess { get; set; }
    public IReadOnlyList<Release> Releases { get; set; }
    public Release NewRelease { get; set; }
    public bool Ready { get; set; }

    public InternalState Clone()
    {
        return (InternalState)MemberwiseClone();
    }
}
