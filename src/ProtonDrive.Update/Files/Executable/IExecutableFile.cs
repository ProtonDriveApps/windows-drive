using System.Diagnostics;

namespace ProtonDrive.Update.Files.Executable;

public interface IExecutableFile
{
    void Execute(string filename, string? arguments, ProcessWindowStyle windowStyle = ProcessWindowStyle.Normal);
}
