namespace Proton.Drive.Sdk.Sync.Client.BugReport;

public interface IBugReportClient
{
    Task SendAsync(BugReportBody report, IReadOnlyCollection<BugReportAttachment> attachments, CancellationToken cancellationToken);
}
