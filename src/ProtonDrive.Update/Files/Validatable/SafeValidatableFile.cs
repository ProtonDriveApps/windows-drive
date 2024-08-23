using System;
using System.Threading.Tasks;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Update.Files.Validatable;

/// <summary>
/// Wraps known exceptions of <see cref="ValidatableFile"/> into <see cref="AppUpdateException"/>.
/// </summary>
internal class SafeValidatableFile : IValidatableFile
{
    private readonly IValidatableFile _origin;

    public SafeValidatableFile(IValidatableFile origin)
    {
        _origin = origin;
    }

    public async Task<bool> IsValidAsync(string filename, ReadOnlyMemory<byte> checksum)
    {
        try
        {
            return await _origin.IsValidAsync(filename, checksum).ConfigureAwait(false);
        }
        catch (Exception e) when (e.IsFileAccessException())
        {
            throw new AppUpdateException("Failed to validate downloaded app update", e);
        }
    }
}
