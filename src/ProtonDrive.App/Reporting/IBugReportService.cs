using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.BugReport;
using ProtonDrive.Shared;

namespace ProtonDrive.App.Reporting;

public interface IBugReportService
{
    public Task<Result> SendAsync(BugReportBody body, bool includeLogs, CancellationToken cancellationToken);
}
