using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;

namespace ProtonDrive.Shared.Diagnostics;

public abstract class BaseSystemProcess : IOsProcess
{
    private readonly ILogger _logger;
    private bool _disposed;

    protected BaseSystemProcess(ILogger logger, Process process)
    {
        _logger = logger;
        Process = process;
    }

    public virtual void Start()
    {
        var processName = GetProcessName(Process.StartInfo.FileName);
        _logger.LogInformation($"Starting new {processName} process");

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

    protected Process Process { get; }

    private string GetProcessName(string executablePath)
    {
        return Path.GetFileNameWithoutExtension(executablePath);
    }
}
