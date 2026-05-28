using Microsoft.Extensions.Logging;
using Proton.Sdk.Telemetry;
using ProtonDrive.Client.Sdk.Metrics;

namespace ProtonDrive.Client.Sdk;

internal sealed class SdkDiagnostics(SdkMetrics metrics, SdkErrorReporting errorReporting, ILoggerFactory loggerFactory) : ITelemetry
{
    public ILogger GetLogger(string name)
    {
        return loggerFactory.CreateLogger(name);
    }

    public void RecordMetric(IMetricEvent metricEvent)
    {
        metrics.Record(metricEvent);
        errorReporting.ReportFileTransferError(metricEvent);
        errorReporting.ReportDecryptionError(metricEvent);
    }
}
