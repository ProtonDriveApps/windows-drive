namespace Proton.Drive.Sdk.Sync.Client.BugReport;

public sealed record BugReportAttachment(string Name, string FileName, Stream Stream);
