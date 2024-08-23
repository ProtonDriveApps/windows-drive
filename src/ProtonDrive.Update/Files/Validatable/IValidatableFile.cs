using System;
using System.Threading.Tasks;

namespace ProtonDrive.Update.Files.Validatable;

internal interface IValidatableFile
{
    Task<bool> IsValidAsync(string filename, ReadOnlyMemory<byte> checksum);
}
