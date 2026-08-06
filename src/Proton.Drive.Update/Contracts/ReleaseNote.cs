namespace Proton.Drive.Update.Contracts;

public class ReleaseNote
{
    public ReleaseNoteType Type { get; set; }
    public IEnumerable<string> Notes { get; set; } = new List<string>();
}
