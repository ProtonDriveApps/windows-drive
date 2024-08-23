using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ProtonDrive.App.Mapping.Setup;

public sealed class NumberSuffixedNameGenerator
{
    private const char EmptySpaceCharacter = ' ';
    private const char DotCharacter = '.';
    private const char ReplacementCharacter = '_';
    private const char SuffixCharacter = '~';

    private readonly HashSet<char> _invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
    private readonly HashSet<string> _reservedNames = new(
        new[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        },
        StringComparer.OrdinalIgnoreCase);

    private readonly string _fullName;
    private readonly int _maxLength;
    private readonly NameType _type;

    public NumberSuffixedNameGenerator(string fullName, NameType type, int maxLength = 255)
    {
        _fullName = SanitizeName(fullName);
        _maxLength = maxLength;
        _type = type;
    }

    public IEnumerable<string> GenerateNames()
    {
        var name = Path.GetFileNameWithoutExtension(_fullName.AsSpan());
        var nameMemory = _fullName.AsMemory()[..name.Length];
        var extensionMemory = _fullName.AsMemory()[^(_fullName.Length - name.Length)..];

        var fileExtensionIsValid = false;

        if (_type is NameType.File)
        {
            var extensionLength = extensionMemory.Length - 1;

            // If the file extension is too long,
            // it is considered non-essential and may be subject to trimming or modification when appending the suffix.
            fileExtensionIsValid = extensionLength <= _maxLength / 4;
        }

        for (int index = 0; index < int.MaxValue; index++)
        {
            if (_type is NameType.File)
            {
                yield return fileExtensionIsValid ? GetFileName(index, nameMemory, extensionMemory) : GetFolderName(index);
                continue;
            }

            yield return GetFolderName(index);
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

    private string GetFileName(int index, ReadOnlyMemory<char> fileName, ReadOnlyMemory<char> fileExtension)
    {
        if (index == 0)
        {
            return _fullName.Length > _maxLength
                ? $"{_fullName[..(_maxLength - fileExtension.Length)]}{fileExtension.Span}"
                : _fullName;
        }

        var suffixLength = GetSuffixLength(index);
        var filenameLength = _maxLength - fileExtension.Length - suffixLength;

        return _fullName.Length + suffixLength > _maxLength
            ? $"{_fullName[..filenameLength]}{(filenameLength > 0 ? " " : "")}({index}){fileExtension.Span}"
            : $"{fileName.Span} ({index}){fileExtension.Span}";
    }

    private string GetFolderName(int index)
    {
        if (index == 0)
        {
            return _fullName.Length > _maxLength
                ? _fullName[.._maxLength]
                : _fullName;
        }

        var suffixLength = GetSuffixLength(index);

        return _fullName.Length + suffixLength > _maxLength
            ? $"{_fullName[..(_maxLength - suffixLength)]} ({index})"
            : $"{_fullName} ({index})";
    }

    private string SanitizeName(ReadOnlySpan<char> name)
    {
        Span<char> buffer = stackalloc char[name.Length];

        var index = 0;

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];

            if ((i == 0 && c == EmptySpaceCharacter)
                || _invalidChars.Contains(c))
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

        if (buffer[^1] == DotCharacter)
        {
            buffer[^1] = ReplacementCharacter;
        }

        var sanitizedName = new string(buffer[..index]);

        return _reservedNames.Contains(sanitizedName) ? $"{sanitizedName}{SuffixCharacter}" : sanitizedName;
    }
}
