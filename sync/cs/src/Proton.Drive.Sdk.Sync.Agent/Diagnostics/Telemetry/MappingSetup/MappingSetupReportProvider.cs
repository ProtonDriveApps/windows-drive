using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.MappingSetup;

internal sealed class MappingSetupReportProvider(MappingSetupStatistics mappingStatistics) : IPeriodicTelemetryReportProvider
{
    public IEnumerable<TelemetryEvent> GetPeriodicReport()
    {
        return MappingSetupReportFactory.CreateReport(mappingStatistics.GetMappingDetails());
    }
}
