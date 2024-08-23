using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Client.BugReport;

public interface IBugReportClient
{
    Task SendAsync(BugReportBody report, Stream? attachment, CancellationToken cancellationToken);
}
