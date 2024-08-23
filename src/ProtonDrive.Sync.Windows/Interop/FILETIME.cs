using System;
using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public readonly struct FILETIME
{
    private readonly uint _dwLowDateTime;
    private readonly uint _dwHighDateTime;

    internal FILETIME(long fileTime)
    {
        _dwLowDateTime = (uint)fileTime;
        _dwHighDateTime = (uint)(fileTime >> 32);
    }

    private static DateTime MinDateTimeValueUtc { get; } = DateTime.FromFileTimeUtc(0);
    private static long MaxFileTimeValue { get; } = DateTime.MaxValue.ToFileTimeUtc();

    public static FILETIME FromDateTimeUtc(DateTime value) => new(value > MinDateTimeValueUtc ? value.ToFileTimeUtc() : 0);

    public DateTime ToDateTimeUtc()
    {
        var fileTime = ((long)_dwHighDateTime << 32) | _dwLowDateTime;

        return fileTime <= 0
            ? DateTime.MinValue
            : fileTime <= MaxFileTimeValue
                ? DateTime.FromFileTimeUtc(fileTime)
                : DateTime.MaxValue;
    }
}
