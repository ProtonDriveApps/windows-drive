using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Proton.Drive.Shared.Diagnostics;

public class SystemProcess : SystemProcessBase
{
    public SystemProcess(ILogger logger, Process process)
        : base(logger, process)
    {
    }
}
