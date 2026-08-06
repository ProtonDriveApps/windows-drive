using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Client.Sdk.Metrics;
using Proton.Sdk.Telemetry;

namespace Proton.Drive.Sdk.Sync.Client.Sdk;

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
