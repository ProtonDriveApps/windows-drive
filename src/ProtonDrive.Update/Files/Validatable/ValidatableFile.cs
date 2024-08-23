using System;
using System.IO;
using System.Threading.Tasks;

namespace ProtonDrive.Update.Files.Validatable;

/// <summary>
/// Validates checksum of file.
/// </summary>
internal class ValidatableFile : IValidatableFile
{
    public async Task<bool> IsValidAsync(string filename, ReadOnlyMemory<byte> checksum)
    {
        return Exists(filename) && await IsChecksumValidAsync(filename, checksum).ConfigureAwait(false);
    }

    private static bool Exists(string filename)
    {
        return !string.IsNullOrEmpty(filename) && File.Exists(filename);
    }

    private static async Task<bool> IsChecksumValidAsync(string filename, ReadOnlyMemory<byte> expectedChecksum)
    {
        var checksum = await new FileChecksum(filename).GetValueAsync().ConfigureAwait(false);

        return checksum.Span.SequenceEqual(expectedChecksum.Span);
    }
}
