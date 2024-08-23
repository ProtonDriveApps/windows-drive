using System;
using System.Collections.Generic;
using ProtonDrive.Update.Contracts;

namespace ProtonDrive.Update;

public interface IRelease
{
    /// <summary>
    /// The release version number
    /// </summary>
    Version Version { get; }

    /// <summary>
    /// The date of the release
    /// </summary>
    DateTime ReleaseDate { get; }

    /// <summary>
    /// Indicates whether this release is released as early access release
    /// </summary>
    bool IsEarlyAccess { get; }

    /// <summary>
    /// Indicates whether this release is newer that the current app
    /// </summary>
    bool IsNew { get; }

    /// <summary>
    /// The list of change descriptions for this release
    /// </summary>
    IReadOnlyList<string> ChangeLog { get; }

    /// <summary>
    /// The list of change descriptions for this release
    /// </summary>
    IReadOnlyList<ReleaseNote> ReleaseNotes { get; }

    /// <summary>
    /// Indicates whether automatic update to this release is disallowed
    /// </summary>
    bool IsAutoUpdateDisabled { get; }
}
