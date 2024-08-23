using System.Diagnostics;

namespace ProtonDrive.Shared.Diagnostics;

public interface IOsProcesses
{
    IOsProcess CreateProcess(string filename, string? arguments = null, ProcessWindowStyle windowStyle = ProcessWindowStyle.Normal);

    IOsProcess CreateElevatedProcess(string filename, string? arguments = null);

    void Open(string filename);
}
