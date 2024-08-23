using System.Runtime.InteropServices;

namespace ProtonDrive.Sync.Windows.Interop;

// https://docs.microsoft.com/en-us/windows/win32/api/subauth/ns-subauth-unicode_string
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public class UNICODE_STRING
{
    /// <summary>
    /// The length, in bytes, of the string pointed to by the <see cref="Buffer"/> member, not including the terminating NULL character, if any.
    /// </summary>
    public ushort Length { get; }

    /// <summary>
    /// The total size, in bytes, of memory allocated for <see cref="Buffer"/>. Up to <see cref="MaximumLength"/> bytes may be written into the buffer without trampling memory.
    /// </summary>
    public ushort MaximumLength { get; }

    /// <summary>
    /// Pointer to a wide-character string.
    /// </summary>
    public string Buffer { get; }

    public UNICODE_STRING(string value)
    {
        Length = (ushort)(value.Length * 2);
        MaximumLength = Length;
        Buffer = value;
    }
}
