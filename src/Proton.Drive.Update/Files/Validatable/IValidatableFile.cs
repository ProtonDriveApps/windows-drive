namespace Proton.Drive.Update.Files.Validatable;

internal interface IValidatableFile
{
    Task<bool> IsValidAsync(string filename, ReadOnlyMemory<byte> checksum);
}
