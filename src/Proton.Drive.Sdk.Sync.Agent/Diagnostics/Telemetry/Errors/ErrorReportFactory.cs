using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.Errors;

internal static class ErrorReportFactory
{
    public static IEnumerable<TelemetryEvent> CreateReport(IReadOnlyDictionary<(string ErrorKey, ErrorScope Scope), int> errorCounts)
    {
        const string countMetricName = "count";
        const string scopeDimensionName = "scope";
        const string errorKeyDimensionName = "errorKey";

        const string measurementGroupName = "drive.windows.errors";
        const string eventName = "periodic_error_count";

        var events = errorCounts.Select(
            x =>
            {
                var (key, count) = x;
                var values = new Dictionary<string, double>();
                var dimensions = new Dictionary<string, string>();

                values.Add(countMetricName, count);
                dimensions.Add(errorKeyDimensionName, key.ErrorKey);
                dimensions.Add(scopeDimensionName, key.Scope.ToString());

                return new TelemetryEvent(measurementGroupName, eventName, values, dimensions);
            });

        return events;
    }
}
