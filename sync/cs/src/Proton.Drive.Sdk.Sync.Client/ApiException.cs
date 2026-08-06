using System.Diagnostics.CodeAnalysis;
using System.Net;
using Proton.Drive.Shared.Client;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Client;

public class ApiException : Exception, IFormattedErrorCodeProvider
{
    public ApiException()
        : this(string.Empty)
    {
    }

    public ApiException(string message)
        : this(ResponseCode.Unknown, message)
    {
    }

    public ApiException(string message, Exception innerException)
        : this(ResponseCode.Unknown, message, innerException)
    {
    }

    public ApiException(ResponseCode responseCode, string message)
        : base(message)
    {
        ResponseCode = responseCode;
    }

    public ApiException(ResponseCode responseCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ResponseCode = responseCode;
    }

    public ApiException(HttpStatusCode httpStatusCode, ResponseCode responseCode, string message, Exception? innerException)
        : base(message, innerException)
    {
        HttpStatusCode = httpStatusCode;
        ResponseCode = responseCode;
    }

    public HttpStatusCode HttpStatusCode { get; }
    public ResponseCode ResponseCode { get; }

    /// <summary>
    /// Indicates whether the exception message comes from the API,
    /// therefore, is suitable for displaying in the UI.
    /// </summary>
    public bool IsMessageAuthoritative { get; init; }

    public bool TryGetRelevantFormattedErrorCode([MaybeNullWhen(false)] out string formattedErrorCode)
    {
        formattedErrorCode = ResponseCode.ToString();

        return true;
    }
}
