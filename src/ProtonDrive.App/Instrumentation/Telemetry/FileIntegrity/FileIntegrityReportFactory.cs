using System.Collections.Immutable;
using ProtonDrive.App.Health;
using ProtonDrive.Client.Instrumentation.Telemetry;
using ProtonDrive.Sync.Shared.Health;

namespace ProtonDrive.App.Instrumentation.Telemetry.FileIntegrity;

internal static class FileIntegrityReportFactory
{
    private const string MeasurementGroupName = "drive.windows.file_integrity";

    public static IEnumerable<TelemetryEvent> CreateReport(IReadOnlyCollection<FileConsistencyGuardFileStatisticsEntry> statistics)
    {
        const string numberOfFilesMetricName = "numberOfFiles";
        const string numberOfBlocksMetricName = "numberOfBlocks";
        const string totalFileSizeInGigaBytesMetricName = "totalFileSizeInGigaBytes";
        const string fileStatusDimensionName = "fileStatus";
        const string reasonDimensionName = "reason";
        const string downloadReasonDimensionName = "downloadReason";
        const string errorDimensionName = "error";
        const string fileCountReportEventName = "periodic_file_count_report";

        return statistics.Select(x =>
        {
            var values = new Dictionary<string, double>();
            var dimensions = new Dictionary<string, string>();

            values.Add(numberOfFilesMetricName, x.NumberOfFiles);
            values.Add(numberOfBlocksMetricName, x.NumberOfBlocks);
            values.Add(totalFileSizeInGigaBytesMetricName, x.TotalFileSizeInGigaBytes);
            dimensions.Add(fileStatusDimensionName, x.Status.ToString());
            dimensions.Add(reasonDimensionName, x.Reason.ToString());
            dimensions.Add(downloadReasonDimensionName, x.DownloadReason.ToString());
            dimensions.Add(errorDimensionName, x.Error.ToString());

            return new TelemetryEvent(MeasurementGroupName, fileCountReportEventName, values, dimensions);
        });
    }

    public static IEnumerable<TelemetryEvent> CreateStatusReport(FileConsistencyGuardStatus? overallStatus)
    {
        const string overallStatusDimensionName = "overallStatus";
        const string statusReportEventName = "periodic_status_report";

        if (overallStatus is null)
        {
            return [];
        }

        var dimensions = new Dictionary<string, string> { { overallStatusDimensionName, overallStatus.Value.ToString() } };

        var overallStatusEvent = new TelemetryEvent(MeasurementGroupName, statusReportEventName, ImmutableDictionary<string, double>.Empty, dimensions);

        return [overallStatusEvent];
    }
}
