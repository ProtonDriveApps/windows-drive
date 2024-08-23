using System;
using System.Collections.Generic;

namespace ProtonDrive.App.Windows.Views.Main.About;

internal sealed class ReleaseNoteViewModel
{
    public Version Version { get; set; } = new();
    public DateTime ReleaseDate { get; set; }
    public ICollection<ReleaseNoteSectionViewModel> Sections { get; set; } = new List<ReleaseNoteSectionViewModel>();
    public bool IsNewVersion { get; set; }
}
