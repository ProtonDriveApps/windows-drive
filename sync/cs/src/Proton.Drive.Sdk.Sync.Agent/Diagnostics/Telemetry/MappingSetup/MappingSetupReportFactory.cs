using Proton.Drive.Sdk.Sync.Client.Contracts;
using Proton.Drive.Sdk.Sync.Shared;
using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.MappingSetup;

internal static class MappingSetupReportFactory
{
    public static IEnumerable<TelemetryEvent> CreateReport(IReadOnlyCollection<MappingSetupDetails> mappingDetails)
    {
        const string countMetricName = "count";
        const string typeDimensionName = "type";
        const string statusDimensionName = "status";
        const string setupStatusDimensionName = "setupStatus";
        const string syncModeDimensionName = "syncMode";
        const string syncMethodDimensionName = "syncMethod";

        const string measurementGroupName = "drive.windows.mappings";
        const string eventName = "periodic_setup_state_report";

        var events = mappingDetails
            .GroupBy(
                x => new
                {
                    x.Type,
                    x.LinkType,
                    x.SyncMethod,
                    x.SyncType,
                    x.Status,
                    x.SetupStatus,
                })
            .Select(
                group =>
                {
                    var values = new Dictionary<string, double>();
                    var dimensions = new Dictionary<string, string>();

                    var type = group.Key.Type is not MappingType.SharedWithMeItem
                        ? group.Key.Type.ToString()
                        : group.Key.LinkType is LinkType.File
                            ? "SharedWithMeFile"
                            : "SharedWithMeFolder";

                    values.Add(countMetricName, group.Count());
                    dimensions.Add(typeDimensionName, type);
                    dimensions.Add(syncMethodDimensionName, group.Key.SyncMethod.ToString());
                    dimensions.Add(syncModeDimensionName, group.Key.SyncType.ToString());
                    dimensions.Add(statusDimensionName, group.Key.Status.ToString());
                    dimensions.Add(setupStatusDimensionName, group.Key.SetupStatus.ToString());
                    return new TelemetryEvent(measurementGroupName, eventName, values, dimensions);
                });

        return events;
    }
}
