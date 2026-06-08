using ProtonDrive.Shared.IO;

namespace ProtonDrive.Sync.Shared.FileSystem.Integration;

public sealed class NumberSuffixedNameGenerator : INumberSuffixedNameGenerator
{
    private const char EmptySpaceCharacter = ' ';
    private const char DotCharacter = '.';
    private const char ReplacementCharacter = '_';
    private const char SuffixCharacter = '~';

    private static readonly HashSet<char> InvalidCharacters = Path.GetInvalidFileNameChars().ToHashSet();
    private static readonly HashSet<string> DosDeviceNames = PathExtensions.DosDeviceNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
    private static readonly int MinDosDeviceNameLength = DosDeviceNames.Select(x => x.Length).Min();
    private static readonly int MaxDosDeviceNameLength = DosDeviceNames.Select(x => x.Length).Max();

    public IEnumerable<string> GenerateNames(string initialName, NameType type, int maxLength = 255)
    {
        var sanitizedName = SanitizeName(initialName);

        var name = Path.GetFileNameWithoutExtension(sanitizedName.AsSpan());
        var nameMemory = sanitizedName.AsMemory()[..name.Length];
        var extensionMemory = sanitizedName.AsMemory()[^(sanitizedName.Length - name.Length)..];

        var fileExtensionIsValid = false;

        if (type is NameType.File)
        {
            var extensionLength = extensionMemory.Length - 1;

            // If the file extension is too long,
            // it is considered non-essential and may be subject to trimming or modification when appending the suffix.
            fileExtensionIsValid = extensionLength <= maxLength / 4;
        }

        for (int index = 0; index < int.MaxValue; index++)
        {
            yield return type switch
            {
                NameType.File => fileExtensionIsValid ? GetFileName(sanitizedName, index, nameMemory, extensionMemory, maxLength) : GetFolderName(sanitizedName, index, maxLength),
                NameType.Folder => GetFolderName(sanitizedName, index, maxLength),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
            };
        }
    }

    private static int GetSuffixLength(int index)
    {
        if (index < 1)
        {
            return 0;
        }

        // The suffix follows the format " (1)", " (2)", etc.
        // Consequently, we need to account for the length of the index as a string and the remaining part of the suffix:
        // which includes 1. a space, 2. an opening parenthesis, and 3. a closing parenthesis.
        const int remainingSuffixLength = 3;

        var indexLength = 1 + (int)Math.Log10(index);

        return remainingSuffixLength + indexLength;
    }

    private static string GetFileName(string sanitizedName, int index, ReadOnlyMemory<char> fileName, ReadOnlyMemory<char> fileExtension, int maxLength)
    {
        if (index == 0)
        {
            return sanitizedName.Length > maxLength
                ? $"{sanitizedName[..(maxLength - fileExtension.Length)]}{fileExtension.Span}"
                : sanitizedName;
        }

        var suffixLength = GetSuffixLength(index);
        var filenameLength = maxLength - fileExtension.Length - suffixLength;

        return sanitizedName.Length + suffixLength > maxLength
            ? $"{sanitizedName[..filenameLength]}{(filenameLength > 0 ? " " : "")}({index}){fileExtension.Span}"
            : $"{fileName.Span} ({index}){fileExtension.Span}";
    }

    private static string GetFolderName(string sanitizedName, int index, int maxLength)
    {
        if (index == 0)
        {
            return sanitizedName.Length > maxLength
                ? sanitizedName[..maxLength]
                : sanitizedName;
        }

        var suffixLength = GetSuffixLength(index);

        return sanitizedName.Length + suffixLength > maxLength
            ? $"{sanitizedName[..(maxLength - suffixLength)]} ({index})"
            : $"{sanitizedName} ({index})";
    }

    private static string SanitizeName(ReadOnlySpan<char> name)
    {
        Span<char> buffer = stackalloc char[name.Length];

        var index = 0;

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if ((i == 0 && c == EmptySpaceCharacter)
                || InvalidCharacters.Contains(c))
            {
                buffer[index++] = ReplacementCharacter;
            }
            else
            {
                buffer[index++] = c;
            }
        }

        if (index == 0)
        {
            return ReplacementCharacter.ToString();
        }

        if (buffer[index - 1] == DotCharacter)
        {
            buffer[index - 1] = ReplacementCharacter;
        }

        var sanitizedName = new string(buffer[..index]);

        if (index >= MinDosDeviceNameLength && index <= MaxDosDeviceNameLength && DosDeviceNames.Contains(sanitizedName))
        {
            return $"{sanitizedName}{SuffixCharacter}";
        }

        if (index >= MinDosDeviceNameLength && sanitizedName.IndexOf('.') is var dotIndex && dotIndex >= MinDosDeviceNameLength &&
            dotIndex <= MaxDosDeviceNameLength && DosDeviceNames.Contains(sanitizedName[..dotIndex]))
        {
            return string.Concat(buffer[..dotIndex], [SuffixCharacter], buffer[dotIndex..index]);
        }

        return sanitizedName;
    }
}
