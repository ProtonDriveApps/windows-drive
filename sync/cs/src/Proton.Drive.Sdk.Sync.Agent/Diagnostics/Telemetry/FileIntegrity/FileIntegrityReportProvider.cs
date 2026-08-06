using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.FileIntegrity;

internal sealed class FileIntegrityReportProvider(FileIntegrityStatistics fileIntegrityStatistics) : IPeriodicTelemetryReportProvider
{
    public IEnumerable<TelemetryEvent> GetPeriodicReport()
    {
        var fileIntegrityReport = FileIntegrityReportFactory.CreateReport(fileIntegrityStatistics.GetDetails());

        var fileIntegrityStatusReport = FileIntegrityReportFactory.CreateStatusReport(fileIntegrityStatistics.GetOverallStatus());

        return [.. fileIntegrityReport, .. fileIntegrityStatusReport];
    }
}
