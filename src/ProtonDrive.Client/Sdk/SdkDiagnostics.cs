using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Telemetry;
using Proton.Sdk;
using Proton.Sdk.Telemetry;
using ProtonDrive.Client.Sdk.Metrics;
using ProtonDrive.Shared.Reporting;
using ProtonDrive.Sync.Shared.FileSystem;

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

    private static bool HttpClientSideErrorIsWorthReporting(Exception originalError)
    {
        var exception = originalError;

        while (exception != null)
        {
            if (exception is ProtonApiException
                {
                    Code: Proton.Sdk.Api.ResponseCode.TooManyChildren
                    or Proton.Sdk.Api.ResponseCode.InsufficientQuota
                    or Proton.Sdk.Api.ResponseCode.InsufficientSpace,
                })
            {
                return false;
            }

            exception = exception.InnerException;
        }

        return true;
    }

    private void ReportFileTransferError(IMetricEvent metricEvent)
    {
        switch (metricEvent)
        {
            case UploadEvent { Error: UploadError.Unknown } uploadEvent:
                errorReporting.CaptureException(SdkFileUploadUnknownErrorException.CreateInstance(uploadEvent.OriginalError));
                break;

            case UploadEvent { Error: UploadError.HttpClientSideError, OriginalError: not null } uploadEvent
                when HttpClientSideErrorIsWorthReporting(uploadEvent.OriginalError):

                FileSystemErrorCode? errorCode = ExceptionMapping.TryMapException(uploadEvent.OriginalError, null, false, out var mappedException)
                    ? mappedException.ErrorCode
                    : null;

                errorReporting.CaptureException(SdkFileUploadHttpClientErrorException.CreateInstance(uploadEvent.OriginalError, errorCode));
                break;

            case DownloadEvent { Error: DownloadError.Unknown } downloadEvent:
                errorReporting.CaptureException(SdkFileDownloadUnknownErrorException.CreateInstance(downloadEvent.OriginalError));
                break;
        }
    }
}
