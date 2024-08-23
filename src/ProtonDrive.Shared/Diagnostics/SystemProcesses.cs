using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ProtonDrive.Shared.Diagnostics;

public class SystemProcesses : IOsProcesses
{
    private readonly ILogger<SystemProcesses> _logger;

    public SystemProcesses(ILogger<SystemProcesses> logger)
    {
        _logger = logger;
    }

    public IOsProcess CreateProcess(string filename, string? arguments, ProcessWindowStyle windowStyle)
    {
        return new SystemProcess(_logger, CreateProcessInternal(filename, arguments, windowStyle));
    }

    public IOsProcess CreateElevatedProcess(string filename, string? arguments)
    {
        return new SystemProcess(_logger, CreateElevatedProcessInternal(filename, arguments));
    }

    public void Open(string filename)
    {
        var startInfo = new ProcessStartInfo(filename)
        {
            UseShellExecute = true,
            Verb = "open",
        };

        Process.Start(startInfo);
    }

    private Process CreateProcessInternal(string filename, string? arguments, ProcessWindowStyle windowStyle)
    {
        return new Process
        {
            StartInfo = new ProcessStartInfo(filename, arguments ?? string.Empty).StandardInfo(windowStyle),
        };
    }

    private Process CreateElevatedProcessInternal(string filename, string? arguments)
    {
        return new Process
        {
            StartInfo = new ProcessStartInfo(filename, arguments ?? string.Empty).ElevatedInfo(),
        };
    }
}
