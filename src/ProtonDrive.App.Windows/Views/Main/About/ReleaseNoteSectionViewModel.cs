using System.Collections.Generic;
using ProtonDrive.Update.Contracts;

namespace ProtonDrive.App.Windows.Views.Main.About;

internal sealed class ReleaseNoteSectionViewModel
{
    public ReleaseNoteType Type { get; set; }
    public ICollection<string> Notes { get; set; } = new List<string>();
}
