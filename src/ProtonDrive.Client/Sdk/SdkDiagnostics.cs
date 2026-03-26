using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Telemetry;
using Proton.Sdk.Telemetry;
using ProtonDrive.Client.Sdk.Metrics;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Reporting;

namespace ProtonDrive.Client.Sdk;

internal sealed class SdkDiagnostics(SdkMetrics metrics, IErrorReporting errorReporting, ILoggerFactory loggerFactory) : ITelemetry
{
    public ILogger GetLogger(string name)
    {
        return loggerFactory.CreateLogger(name);
    }

    public void RecordMetric(IMetricEvent metricEvent)
    {
        metrics.Record(metricEvent);
        ReportUnknownFileTransferError(metricEvent);
    }

    private void ReportUnknownFileTransferError(IMetricEvent metricEvent)
    {
        switch (metricEvent)
        {
            case UploadEvent { Error: UploadError.Unknown } uploadEvent:
                errorReporting.CaptureError($"Drive SDK upload Unknown error: {uploadEvent.OriginalError?.CombinedMessage()}");
                break;

            case DownloadEvent { Error: DownloadError.Unknown } downloadEvent:
                errorReporting.CaptureError($"Drive SDK download Unknown error: {downloadEvent.OriginalError?.CombinedMessage()}");
                break;
        }
    }
}
