namespace Proton.Drive.Sdk.Sync.Windows.FileSystem;

public enum FileSystemNameValidationResult
{
    Valid,
    Empty,
    TooLong,
    ContainsInvalidCharacter,
    EndsWithSpace,
    EndsWithPeriod,
    Reserved,
}
