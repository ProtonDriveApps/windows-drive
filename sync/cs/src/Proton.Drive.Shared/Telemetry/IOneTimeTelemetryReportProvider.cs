namespace Proton.Drive.Shared.Telemetry;

public interface IOneTimeTelemetryReportProvider
{
    IEnumerable<TelemetryEvent> GetOneTimeReport();

    void OnOneTimeReportSent();
}
