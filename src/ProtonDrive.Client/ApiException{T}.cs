using System;
using System.Net;

namespace ProtonDrive.Client;

public sealed class ApiException<T> : ApiException
{
    public ApiException()
        : this(string.Empty)
    {
    }

    public ApiException(string message)
        : this(ResponseCode.Unknown, message, default)
    {
    }

    public ApiException(string message, Exception innerException)
        : this(ResponseCode.Unknown, message, default, innerException)
    {
    }

    public ApiException(string message, T? content)
        : base(message)
    {
        Content = content;
    }

    public ApiException(string message, T? content, Exception innerException)
        : base(message, innerException)
    {
        Content = content;
    }

    public ApiException(ResponseCode responseCode, string message, T? content)
        : base(responseCode, message)
    {
        Content = content;
    }

    public ApiException(ResponseCode responseCode, string message, T? content, Exception innerException)
        : base(responseCode, message, innerException)
    {
        Content = content;
    }

    public ApiException(HttpStatusCode httpStatusCode, ResponseCode responseCode, string message, T? content, Exception innerException)
        : base(httpStatusCode, responseCode, message, innerException)
    {
        Content = content;
    }

    public T? Content { get; set; }
}
