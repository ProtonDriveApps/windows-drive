using System.Diagnostics;

namespace Proton.Drive.Update.Files.Executable;

public interface IExecutableFile
{
    void Execute(string filename, string? arguments, ProcessWindowStyle windowStyle = ProcessWindowStyle.Normal);
}
