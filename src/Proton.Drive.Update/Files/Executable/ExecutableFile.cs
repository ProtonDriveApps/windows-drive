using System.Diagnostics;
using Proton.Drive.Shared.Diagnostics;

namespace Proton.Drive.Update.Files.Executable;

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
