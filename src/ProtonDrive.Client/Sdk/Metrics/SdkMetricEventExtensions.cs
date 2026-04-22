using Proton.Drive.Sdk.Telemetry;
using Proton.Sdk;

namespace ProtonDrive.Client.Sdk.Metrics;

internal static class SdkMetricEventExtensions
{
    public static bool ErrorCanBeIgnored(this UploadEvent uploadEvent)
    {
        if (uploadEvent.Error is null)
        {
            return true;
        }

        var uploadError = uploadEvent.Error.Value;
        var originalError = uploadEvent.OriginalError;

        if (uploadError is not UploadError.HttpClientSideError)
        {
            return false;
        }

        var exception = originalError;

        while (exception != null)
        {
            if (exception is ProtonApiException { Code: Proton.Sdk.Api.ResponseCode.TooManyChildren })
            {
                return true;
            }

            exception = exception.InnerException;
        }

        return false;
    }
}
