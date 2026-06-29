using Proton.Drive.Sdk.Sync.Client.BugReport;
using Proton.Drive.Shared;

namespace Proton.Drive.App.Reporting;

public interface IBugReportService
{
    public Task<Result> SendAsync(BugReportBody body, bool includeLogs, CancellationToken cancellationToken);
}
