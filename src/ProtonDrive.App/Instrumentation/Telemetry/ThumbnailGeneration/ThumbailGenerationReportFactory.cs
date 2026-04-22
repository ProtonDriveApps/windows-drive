using ProtonDrive.Client.Instrumentation.Telemetry;

namespace ProtonDrive.App.Instrumentation.Telemetry.ThumbnailGeneration;

internal static class ThumbailGenerationReportFactory
{
    private const string MeasurementGroupName = "drive.windows.thumbnails";

    public static IEnumerable<TelemetryEvent> CreateReport(IReadOnlyCollection<ThumbnailGenerationStatistics> statistics)
    {
        const string metricName = "generation_count";
        const string countValueName = "count";
        const string resultDimensionName = "result";
        const string typeDimensionName = "type";
        const string methodDimensionName = "method";
        const string fileExtensionDimensionName = "fileExtension";
        const string fileSizeRangeDimentionName = "fileSizeRange";
        const string durationRangeDimentionName = "durationRange";

        return statistics.Select(x =>
        {
            var values = new Dictionary<string, double>();
            var dimensions = new Dictionary<string, string>();

            values.Add(countValueName, x.Count);
            dimensions.Add(resultDimensionName, x.Result.ToString());
            dimensions.Add(typeDimensionName, x.Type.ToString());
            dimensions.Add(methodDimensionName, x.Method.ToString());
            dimensions.Add(fileExtensionDimensionName, x.FileExtension);
            dimensions.Add(fileSizeRangeDimentionName, x.FileSizeRange.ToString());
            dimensions.Add(durationRangeDimentionName, x.DurationRange.ToString());
            return new TelemetryEvent(MeasurementGroupName, metricName, values, dimensions);
        });
    }
}
