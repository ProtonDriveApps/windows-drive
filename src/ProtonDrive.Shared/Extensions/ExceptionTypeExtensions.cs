using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

namespace ProtonDrive.Shared.Extensions;

public static class ExceptionTypeExtensions
{
#pragma warning disable SA1310
    // ReSharper disable InconsistentNaming
    private const int E_FAIL = unchecked((int)0x80004005);
    private const int COR_E_IO = unchecked((int)0x80131620);
    private const int COR_E_SYSTEM = unchecked((int)0x80131501);
    private const int COR_E_EXCEPTION = unchecked((int)0x80131500);
    // ReSharper restore InconsistentNaming
#pragma warning restore SA1310

    private enum ErrorCodeFormat
    {
        Decimal,
        Hexadecimal,
        Adaptive,
    }

    public static bool IsExpectedExceptionOf(this Exception ex, object origin) =>
        ((IThrowsExpectedExceptions)origin).IsExpectedException(ex);

    public static bool IsFileAccessException(this Exception ex) =>
        ex is IOException or UnauthorizedAccessException;

    public static bool HResultContainsWin32ErrorCode(this Exception ex, int errorCode)
    {
        return IsWin32Error() && (ex.HResult & 0x0000FFFF) == errorCode;

        bool IsWin32Error() => (ex.HResult & 0xA7FF0000) == 0x80070000;
    }

    public static string? GetRelevantFormattedErrorCode(this Exception ex)
    {
        return TryGetRelevantFormattedErrorCode(ex, out var errorCode) ? errorCode : default;
    }

    public static bool TryGetRelevantFormattedErrorCode(this Exception ex, [MaybeNullWhen(false)] out string formattedErrorCode)
    {
        return ex switch
        {
            IErrorCodeProvider errorCodeProvider
                => errorCodeProvider.TryGetRelevantFormattedErrorCode(out formattedErrorCode),

            Win32Exception win32Exception
                => TryFormatErrorCode(win32Exception.NativeErrorCode, 0, ErrorCodeFormat.Decimal, out formattedErrorCode),

            IOException
                => TryFormatErrorCode(ex.HResult, COR_E_IO, ErrorCodeFormat.Hexadecimal, out formattedErrorCode),

            ExternalException externalException
                => TryFormatErrorCode(externalException.ErrorCode, E_FAIL, ErrorCodeFormat.Adaptive, out formattedErrorCode),

            SystemException
                => TryFormatErrorCode(ex.HResult, COR_E_SYSTEM, ErrorCodeFormat.Hexadecimal, out formattedErrorCode),

            _ => TryFormatErrorCode(ex.HResult, COR_E_EXCEPTION, ErrorCodeFormat.Hexadecimal, out formattedErrorCode),
        };

        static bool TryFormatErrorCode(int errorCode, int errorCodeToIgnore, ErrorCodeFormat format, [MaybeNullWhen(false)] out string formattedErrorCode)
        {
            if (errorCode == errorCodeToIgnore)
            {
                formattedErrorCode = null;
                return false;
            }

            formattedErrorCode = format switch
            {
                ErrorCodeFormat.Decimal => errorCode.ToString(),
                ErrorCodeFormat.Hexadecimal => $"0x{errorCode:X8}",
                _ => IsBetterFormattedAsHex(errorCode) ? $"0x{errorCode:X8}" : errorCode.ToString(),
            };

            return true;
        }

        static bool IsBetterFormattedAsHex(int errorCode)
        {
            // If the first bit is set to 1, it is likely to be the severity bit of an HRESULT which is usually displayed in hex format.
            return (errorCode & 0x80000000) != 0;
        }
    }
}
