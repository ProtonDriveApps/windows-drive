namespace ProtonDrive.Sync.Shared.FileSystem.Integration;

public interface INumberSuffixedNameGenerator
{
    IEnumerable<string> GenerateNames(string initialName, NameType type, int maxLength = 255);
}
