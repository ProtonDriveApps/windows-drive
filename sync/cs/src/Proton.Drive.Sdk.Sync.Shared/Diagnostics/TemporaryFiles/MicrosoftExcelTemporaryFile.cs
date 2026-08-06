namespace Proton.Drive.Sdk.Sync.Shared.Diagnostics.TemporaryFiles;

public static class MicrosoftExcelTemporaryFile
{
    public static bool IsCandidate(string filename, FileAttributes attributes = FileAttributes.None)
    {
        return !attributes.HasFlag(FileAttributes.Directory) && IsEightCharacterHexadecimal(filename);
    }

    private static bool IsEightCharacterHexadecimal(ReadOnlySpan<char> text)
    {
        if (text.Length != 8)
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
