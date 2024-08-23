using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal static class HResultExtensions
{
#pragma warning disable SA1310
    private const int ERROR_CLOUD_FILE_REQUEST_CANCELED = 398;
#pragma warning restore SA1310

    [DebuggerStepThrough]
    [DebuggerHidden]
    public static HRESULT ThrowExceptionForHR(this HRESULT hr)
    {
        if (hr is { Facility: HRESULT.FacilityCode.FACILITY_WIN32, Code: ERROR_CLOUD_FILE_REQUEST_CANCELED })
        {
            throw new OperationCanceledException();
        }

        Marshal.ThrowExceptionForHR((int)hr);

        return hr;
    }
}
