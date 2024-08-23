using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ProtonDrive.Shared.Diagnostics;

public class SystemProcess : BaseSystemProcess
{
    public SystemProcess(ILogger logger, Process process)
        : base(logger, process)
    {
    }
}
