using Proton.Sdk.Api.Http;

namespace Proton.Drive.Sdk.Sync.Client.Configuration;

internal static class HttpRequestMessageExtensions
{
    public static bool GetRetryIsDisabled(this HttpRequestMessage requestMessage)
    {
        return requestMessage.GetRequestType() is HttpRequestType.StorageDownload or HttpRequestType.StorageUpload;
    }
}
