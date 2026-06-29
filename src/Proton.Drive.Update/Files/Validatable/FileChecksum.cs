using System.Security.Cryptography;

namespace Proton.Drive.Update.Files.Validatable;

/// <summary>
/// Calculates SHA512 checksum of file.
/// </summary>
internal class FileChecksum
{
    private const int FileBufferSize = 16768;
    private readonly string _filename;

    public FileChecksum(string filename)
    {
        _filename = filename;
    }

    public async Task<ReadOnlyMemory<byte>> GetValueAsync()
    {
        using var sha512 = SHA512.Create();

        var stream = new FileStream(_filename, FileMode.Open, FileAccess.Read, FileShare.Read, FileBufferSize, true);
        await using (stream.ConfigureAwait(false))
        {
            await sha512.ComputeHashAsync(stream, CancellationToken.None).ConfigureAwait(false);
        }

        return sha512.Hash;
    }
}
