using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Sockets;

namespace ProtonDrive.Update.Helpers;

internal static class ExceptionExtensions
{
    public static bool IsCommunicationException(this Exception ex)
    {
        return ex is HttpRequestException ||
               ex is OperationCanceledException ||
               ex is SocketException ||
               ex is TimeoutException;
    }

    public static bool IsProcessException(this Exception ex)
    {
        return ex is Win32Exception;
    }
}
