using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ProtonDrive.Shared.Diagnostics;

public abstract class SystemProcessBase : IOsProcess
{
    private readonly ILogger _logger;
    private bool _disposed;

    protected SystemProcessBase(ILogger logger, Process process)
    {
        _logger = logger;
        Process = process;
    }

    protected Process Process { get; }

    public virtual void Start()
    {
        var processName = GetProcessName(Process.StartInfo.FileName);
        _logger.LogInformation("Starting new \"{ProcessName}\" process", processName);

        Process.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Process.Dispose();

        _disposed = true;
    }

    private static string GetProcessName(string executablePath)
    {
        return Path.GetFileName(executablePath);
    }
}
