using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using ProtonDrive.Shared.Telemetry;

namespace ProtonDrive.Client.Telemetry;

public sealed record TelemetryEvent(
    string MeasurementGroup,
    [property:JsonPropertyName("Event")]
    string EventName,
    IReadOnlyDictionary<string, double> Values,
    IReadOnlyDictionary<string, string> Dimensions)
{
    public static TelemetryEvent CreatePeriodicReportEvent(IReadOnlyDictionary<string, double> values, IReadOnlyDictionary<string, string> dimensions)
    {
        const string measurementGroupName = "drive.windows.health";
        const string eventName = "periodic_report";

        return new TelemetryEvent(measurementGroupName, eventName, values, dimensions);
    }

    public static TelemetryEvents CreatePeriodicErrorCountEvent(IReadOnlyDictionary<(string ErrorKey, ErrorScope Scope), int> errorCounts)
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

        return new TelemetryEvents(events.ToList().AsReadOnly());
    }
}
