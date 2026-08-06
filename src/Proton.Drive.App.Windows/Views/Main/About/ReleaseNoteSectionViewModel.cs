using Proton.Drive.Update.Contracts;

namespace Proton.Drive.App.Windows.Views.Main.About;

internal sealed class ReleaseNoteSectionViewModel
{
    public ReleaseNoteType Type { get; set; }
    public ICollection<string> Notes { get; set; } = new List<string>();
}
