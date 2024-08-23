using System;
using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public readonly struct LONG_FILETIME
{
    private readonly long _fileTime;

    internal LONG_FILETIME(long value)
    {
        _fileTime = value;
    }

    public bool IsDefault => _fileTime == default;

    private static DateTime MinDateTimeValueUtc { get; } = DateTime.FromFileTimeUtc(0);
    private static long MaxFileTimeValue { get; } = DateTime.MaxValue.ToFileTimeUtc();

    public static LONG_FILETIME FromDateTimeUtc(DateTime value) => new(value > MinDateTimeValueUtc ? value.ToFileTimeUtc() : 0);

    public DateTime ToDateTimeUtc() => _fileTime <= 0
        ? DateTime.MinValue
        : _fileTime <= MaxFileTimeValue
            ? DateTime.FromFileTimeUtc(_fileTime)
            : DateTime.MaxValue;

    public FILETIME ToFileTime() => new(_fileTime);
}
