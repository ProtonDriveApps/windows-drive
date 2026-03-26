using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ProtonDrive.Shared.Diagnostics;

public class SystemProcess : SystemProcessBase
{
    public SystemProcess(ILogger logger, Process process)
        : base(logger, process)
    {
    }
}
