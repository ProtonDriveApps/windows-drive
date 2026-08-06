using Proton.Sdk.Http;

namespace Proton.Drive.Sdk.Sync.Client.Configuration;

internal static class HttpRequestMessageExtensions
{
    public static bool GetRetryIsDisabled(this HttpRequestMessage requestMessage)
    {
        return requestMessage.GetRequestType() is HttpRequestType.StorageDownload or HttpRequestType.StorageUpload;
    }

    private static HttpRequestType GetRequestType(this HttpRequestMessage requestMessage)
    {
        return requestMessage.Options.TryGetValue(HttpRequestOptionKeys.RequestType, out var requestType) ? requestType : HttpRequestType.RegularApi;
    }
}
