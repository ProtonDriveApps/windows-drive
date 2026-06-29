using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Telemetry;
using Proton.Drive.Shared.Reporting;
using Proton.Sdk;
using Proton.Sdk.Telemetry;

namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal sealed class SdkErrorReporting(IErrorReporting errorReporting, ILogger<SdkErrorReporting> logger)
{
    public void ReportFileTransferError(IMetricEvent metricEvent)
    {
        switch (metricEvent)
        {
            case UploadEvent { Error: UploadError.Unknown } uploadEvent:
                errorReporting.CaptureException(SdkFileUploadUnknownErrorException.Create(uploadEvent.OriginalError));
                break;

            case UploadEvent { Error: UploadError.HttpClientSideError, OriginalError: not null } uploadEvent
                when HttpClientSideErrorIsWorthReporting(uploadEvent.OriginalError):

                FileSystemErrorCode? errorCode = ExceptionMapping.TryMapException(uploadEvent.OriginalError, null, false, out var mappedException)
                    ? mappedException.ErrorCode
                    : null;

                errorReporting.CaptureException(SdkFileUploadHttpClientErrorException.Create(uploadEvent.OriginalError, errorCode));
                break;

            case DownloadEvent { Error: DownloadError.Unknown } downloadEvent:
                errorReporting.CaptureException(SdkFileDownloadUnknownErrorException.Create(downloadEvent.OriginalError));
                break;
        }
    }

    public void ReportDecryptionError(IMetricEvent metricEvent)
    {
        if (metricEvent is not DecryptionErrorEvent decryptionErrorEvent)
        {
            return;
        }

        var errorMessage =
            $"{decryptionErrorEvent.Error}, \r\n" +
            $"Volume: {decryptionErrorEvent.VolumeType}, \r\n" +
            $"Field: {decryptionErrorEvent.Field}, \r\n" +
            $"From before 2024: {decryptionErrorEvent.FromBefore2024}, \r\n" +
            $"Uid: \"{decryptionErrorEvent.Uid}\"";

        logger.LogWarning("Drive SDK decryption error: {ErrorDetails}", errorMessage.Replace("\r\n", string.Empty));

        errorReporting.CaptureException(new SdkDecryptionErrorException(errorMessage));
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
}
