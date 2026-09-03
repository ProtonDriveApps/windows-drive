using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Telemetry;
using Proton.Drive.Shared.Client;
using Proton.Drive.Shared.Reporting;
using Proton.Sdk.Api;
using Proton.Sdk.Telemetry;

namespace Proton.Drive.Sdk.Sync.Client.Sdk;

internal sealed class SdkErrorReporting(IErrorReporting errorReporting, ILogger<SdkErrorReporting> logger)
{
    public void ReportFileTransferError(IMetricEvent metricEvent)
    {
        switch (metricEvent)
        {
            case UploadEvent { Error: UploadError.HttpClientSideError, OriginalError: not null } uploadEvent
                when HttpClientSideErrorIsWorthReporting(uploadEvent.OriginalError):

                FileSystemErrorCode? errorCode = ExceptionMapping.TryMapException(uploadEvent.OriginalError, null, false, out var mappedException)
                    ? mappedException.ErrorCode
                    : null;

                errorReporting.CaptureException(SdkFileUploadHttpClientErrorException.Create(uploadEvent.OriginalError, errorCode), ErrorTag.SdkUploadError);
                break;

            case UploadEvent { Error: UploadError.IntegrityError } uploadEvent:
                errorReporting.CaptureException(SdkFileUploadUnknownErrorException.Create(uploadEvent.OriginalError), ErrorTag.SdkIntegrityUploadError);
                break;

            case UploadEvent { Error: UploadError.Unknown } uploadEvent:
                errorReporting.CaptureException(SdkFileUploadUnknownErrorException.Create(uploadEvent.OriginalError), ErrorTag.SdkUploadError);
                break;

            case DownloadEvent { Error: DownloadError.Unknown } downloadEvent:
                errorReporting.CaptureException(SdkFileDownloadUnknownErrorException.Create(downloadEvent.OriginalError), ErrorTag.SdkDownloadError);
                break;

            case DownloadEvent { Error: DownloadError.IntegrityError } downloadEvent:
                errorReporting.CaptureException(SdkFileDownloadUnknownErrorException.Create(downloadEvent.OriginalError), ErrorTag.SdkIntegrityDownloadError);
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

        errorReporting.CaptureException(new SdkDecryptionErrorException(errorMessage), ErrorTag.SdkDecryptionError);
    }

    private static bool HttpClientSideErrorIsWorthReporting(Exception originalError)
    {
        var exception = originalError;

        while (exception != null)
        {
            if (exception is ProtonApiException
                {
                    Code: (int)ResponseCode.TooManyChildren
                    or (int)ResponseCode.InsufficientQuota
                    or (int)ResponseCode.InsufficientSpace,
                })
            {
                return false;
            }

            exception = exception.InnerException;
        }

        return true;
    }
}
