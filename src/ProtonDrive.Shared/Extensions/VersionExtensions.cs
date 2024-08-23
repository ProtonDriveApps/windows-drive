using System;

namespace ProtonDrive.Shared.Extensions;

public static class VersionExtensions
{
    public static Version ToNormalized(this Version version)
        => new Version(version.Major, version.Minor, version.Build >= 0 ? version.Build : 0);
}
