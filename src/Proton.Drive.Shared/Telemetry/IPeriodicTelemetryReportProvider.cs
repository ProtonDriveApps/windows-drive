namespace Proton.Drive.Shared.Telemetry;

public interface IPeriodicTelemetryReportProvider
{
    IEnumerable<TelemetryEvent> GetPeriodicReport();

    void Reset()
    {
    }
}
