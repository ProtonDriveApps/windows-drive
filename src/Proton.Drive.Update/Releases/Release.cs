using Proton.Drive.Update.Contracts;

namespace Proton.Drive.Update.Releases;

/// <summary>
/// A release of the app in the release history.
/// </summary>
internal class Release : IRelease, IComparable, IComparable<IRelease>
{
    private readonly ReleaseContract _release;

    public Release(ReleaseContract release, bool earlyAccess, Version currentVersion)
    {
        _release = release;
        Version = Version.Parse(_release.Version);
        IsEarlyAccess = earlyAccess;
        IsNew = Version > currentVersion;
    }

    public Version Version { get; }

    public DateTime ReleaseDate => _release.ReleaseDate;

    public double? RolloutRatio => _release.RolloutRatio;

    public IReadOnlyList<string> ChangeLog => _release.ChangeLog;

    public IReadOnlyList<ReleaseNote> ReleaseNotes => _release.ReleaseNotes;

    public bool IsAutoUpdateDisabled => _release.IsAutoUpdateDisabled;

    public bool IsEarlyAccess { get; }

    public bool IsNew { get; }

    public FileContract File => _release.File;

    public static Release EmptyRelease()
    {
        return new Release(new ReleaseContract { Version = "0.0.0" }, false, new Version(0, 0, 0));
    }

    public bool IsEmpty()
    {
        return _release.Version == "0.0.0" ||
               string.IsNullOrEmpty(_release.File.Url) ||
               string.IsNullOrEmpty(_release.File.Sha512Checksum);
    }

    public int CompareTo(IRelease? other)
    {
        return Version.CompareTo(other?.Version);
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as IRelease);
    }
}
