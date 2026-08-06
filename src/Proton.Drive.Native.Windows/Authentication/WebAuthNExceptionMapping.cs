using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Proton.Drive.Native.Authentication;

internal static class WebAuthNExceptionMapping
{
    private const uint HResultInvalidData = 0x8007000D;
    private const uint HResultRequestNotSupported = 0x80070032;
    private const uint HResultOperationCancelled = 0x800704C7;
    private const uint HResultOperationTimeout = 0x800705B4;
    private const uint HResultParameterInvalid = 0x80090027;
    private const uint HResultOperationNotSupported = 0x80090029;
    private const uint HResultActionCancelled = 0x80090036;

    public static bool TryMapException(Exception exception, [MaybeNullWhen(false)] out Exception mappedException)
    {
        mappedException = exception switch
        {
            COMException ex when (uint)ex.HResult is HResultInvalidData or HResultParameterInvalid => new ArgumentException(ex.Message, ex),
            COMException ex when (uint)ex.HResult is HResultActionCancelled or HResultOperationCancelled => new OperationCanceledException(ex.Message, ex),
            COMException ex when (uint)ex.HResult is HResultOperationTimeout => new TimeoutException(ex.Message, ex),
            COMException ex when (uint)ex.HResult is HResultRequestNotSupported or HResultOperationNotSupported => new NotSupportedException(ex.Message, ex),
            _ => null,
        };

        return mappedException is not null;
    }
}
