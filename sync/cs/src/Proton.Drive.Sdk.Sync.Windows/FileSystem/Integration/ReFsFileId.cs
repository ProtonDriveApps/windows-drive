namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Integration;

/// <summary>
/// Relation between the 64-bit file ID the sync engine uses and the 128-bit file ID of ReFS.
/// </summary>
/// <remarks>
/// <para>
/// ReFS identifies files by a 128-bit ID. The upper 64 bits hold the ID of the directory the file was
/// created in, the lower 64 bits a per-directory ordinal. The ID survives rename and move, including
/// moves to another directory (the upper half keeps the original directory ID).
/// </para>
/// <para>
/// The 64-bit ID returned by <c>GetFileInformationByHandle</c>, by directory enumeration and accepted by
/// <c>OpenFileById</c> is a packed form of it: the top 4 bits hold the number of hexadecimal digits of the
/// directory ID, the directory ID occupies the lowest bits and the ordinal follows above it. When both
/// parts do not fit into the remaining 60 bits, ReFS returns <see cref="InvalidFileId"/> instead.
/// The packing is therefore lossless, and every 64-bit ID other than <see cref="InvalidFileId"/> is unique.
/// </para>
/// <para>
/// Microsoft does not document this scheme; it was observed on ReFS 3.14 (Windows 11 build 26300).
/// Callers must check it with <see cref="IsLosslessRepresentation"/> before relying on it.
/// </para>
/// </remarks>
public static class ReFsFileId
{
    /// <summary>
    /// Value returned for file system objects whose 128-bit ID has no unique 64-bit representation.
    /// </summary>
    public const long InvalidFileId = -1;

    private const int LengthShift = 60;
    private const ulong PayloadMask = (1UL << LengthShift) - 1;

    /// <summary>
    /// Unpacks a 64-bit ReFS file ID into the two halves of the 128-bit ID.
    /// </summary>
    /// <returns><c>true</c> if the value is a canonical packed ReFS file ID, otherwise <c>false</c>.</returns>
    public static bool TryUnpack(long fileId, out ulong high, out ulong low)
    {
        high = 0;
        low = 0;

        if (fileId == InvalidFileId)
        {
            return false;
        }

        var value = unchecked((ulong)fileId);
        var digits = (int)(value >> LengthShift);

        if (digits == 0)
        {
            return false;
        }

        var bits = digits * 4;
        var directoryMask = (1UL << bits) - 1;

        high = value & directoryMask;

        // Canonical form: the most significant digit of the directory ID is not zero
        if (high >> (bits - 4) == 0)
        {
            return false;
        }

        low = (value & PayloadMask) >> bits;

        return true;
    }

    /// <summary>
    /// Checks that the 64-bit file ID is the lossless packed form of the 128-bit file ID.
    /// </summary>
    public static bool IsLosslessRepresentation(long fileId, ulong fileIdHigh, ulong fileIdLow)
    {
        return TryUnpack(fileId, out var high, out var low) && high == fileIdHigh && low == fileIdLow;
    }
}
