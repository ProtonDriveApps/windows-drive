using System.Text.RegularExpressions;

namespace ProtonDrive.Update.Files.Executable;

/// <summary>
/// Validates arguments from the release metadata so they cannot carry unsafe content.
/// </summary>
internal static partial class CommandLineArgumentsValidator
{
    public static bool Validate(string? arguments)
    {
        return string.IsNullOrEmpty(arguments) || ValidArgumentsRegex().IsMatch(arguments);
    }

    [GeneratedRegex(@"^[a-zA-Z0-9/_.\- =]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ValidArgumentsRegex();
}
