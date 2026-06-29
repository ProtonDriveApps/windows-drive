using Proton.Drive.Shared.Telemetry;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Telemetry.Errors;

internal sealed class ErrorReportProvider(IErrorCountProvider errorCountProvider) : IPeriodicTelemetryReportProvider
{
    public IEnumerable<TelemetryEvent> GetPeriodicReport()
    {
        try
        {
            return ErrorReportFactory.CreateReport(errorCountProvider.GetTopErrorCounts(maximumNumberOfCounters: 10));
        }
        finally
        {
            Reset();
        }
    }

    public void Reset()
    {
        errorCountProvider.Reset();
    }
}
