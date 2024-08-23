using System;

namespace ProtonDrive.Client.Cryptography;

internal static class ByteSpanExtensions
{
    public static string ToHexString(this byte[] array) => ToHexString(array.AsSpan());

    public static string ToHexString(this Span<byte> span) => ToHexString((ReadOnlySpan<byte>)span);

    public static string ToHexString(this ReadOnlySpan<byte> span)
    {
        return Convert.ToHexString(span).ToLowerInvariant();
    }
}
