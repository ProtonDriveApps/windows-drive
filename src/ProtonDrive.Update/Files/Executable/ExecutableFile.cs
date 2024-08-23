using System.Diagnostics;
using ProtonDrive.Shared.Diagnostics;

namespace ProtonDrive.Update.Files.Executable;

/// <summary>
/// Starts new process requesting elevation.
/// </summary>
internal class ExecutableFile : IExecutableFile
{
    private readonly IOsProcesses _processes;

    public ExecutableFile(IOsProcesses processes)
    {
        _processes = processes;
    }

    public void Execute(string filename, string? arguments, ProcessWindowStyle windowStyle = ProcessWindowStyle.Normal)
    {
        _processes.CreateProcess(filename, arguments, windowStyle).Start();
    }
}
