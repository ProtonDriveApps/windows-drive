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
        ReportFileTransferError(metricEvent);
    }

    private void ReportFileTransferError(IMetricEvent metricEvent)
    {
        switch (metricEvent)
        {
            case UploadEvent { Error: UploadError.Unknown } uploadEvent:
                errorReporting.CaptureError($"Drive SDK upload Unknown error: {uploadEvent.OriginalError?.CombinedMessage()}");
                break;

            case UploadEvent { Error: UploadError.HttpClientSideError } uploadEvent:

                if (uploadEvent.ErrorCanBeIgnored() || uploadEvent.OriginalError is null)
                {
                    return;
                }

                var errorCode = ExceptionMapping.TryMapException(uploadEvent.OriginalError, null, false, out var mappedException)
                    ? $" ({mappedException.ErrorCode})"
                    : string.Empty;

                errorReporting.CaptureError($"Drive SDK upload HTTP client error:{errorCode} {uploadEvent.OriginalError.CombinedMessage()}");
                break;

            case DownloadEvent { Error: DownloadError.Unknown } downloadEvent:
                errorReporting.CaptureError($"Drive SDK download Unknown error: {downloadEvent.OriginalError?.CombinedMessage()}");
                break;
        }
    }
}
