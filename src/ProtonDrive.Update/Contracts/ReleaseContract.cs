using System;
using System.Collections.Generic;

namespace ProtonDrive.Update.Contracts;

internal class ReleaseContract
{
    public string CategoryName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
    public double? RolloutRatio { get; set; }
    public FileContract File { get; set; } = new();
    public IReadOnlyList<ReleaseNote> ReleaseNotes { get; set; } = new List<ReleaseNote>();
    public bool IsAutoUpdateDisabled { get; set; }

    // To support legacy content
    public IReadOnlyList<string> ChangeLog { get; set; } = new List<string>();
}
