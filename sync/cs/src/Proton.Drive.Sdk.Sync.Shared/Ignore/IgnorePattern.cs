using System.Text;
using System.Text.RegularExpressions;

namespace Proton.Drive.Sdk.Sync.Shared.Ignore;

/// <summary>
/// A single gitignore style pattern compiled into a regular expression.
/// </summary>
/// <remarks>
/// <para>
/// Matching is case insensitive, because the client only supports case insensitive file systems.
/// </para>
/// <para>
/// Both the forward slash and the backslash are accepted as path separators. As a consequence the
/// gitignore escape sequences are only supported for a leading <c>\#</c> and a leading <c>\!</c>.
/// </para>
/// </remarks>
public sealed class IgnorePattern
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private readonly Regex _regex;

    private IgnorePattern(Regex regex, bool isNegated, bool matchesDirectoriesOnly)
    {
        _regex = regex;
        IsNegated = isNegated;
        MatchesDirectoriesOnly = matchesDirectoriesOnly;
    }

    /// <summary>
    /// The pattern starts with "!", a match re-includes the item instead of excluding it.
    /// </summary>
    public bool IsNegated { get; }

    /// <summary>
    /// The pattern ends with "/", it matches directories only.
    /// </summary>
    public bool MatchesDirectoriesOnly { get; }

    /// <summary>
    /// Parses a single line of an ignore file.
    /// </summary>
    /// <returns>The parsed pattern, or null if the line is blank, a comment or malformed.</returns>
    public static IgnorePattern? TryParse(string? line)
    {
        if (line is null)
        {
            return null;
        }

        var value = TrimTrailingUnescapedSpaces(line).TrimStart();

        if (value.Length == 0 || value[0] == '#')
        {
            return null;
        }

        var isNegated = false;

        if (value[0] == '!')
        {
            isNegated = true;
            value = value[1..];
        }
        else if (value.StartsWith("\\!", StringComparison.Ordinal) || value.StartsWith("\\#", StringComparison.Ordinal))
        {
            value = value[1..];
        }

        value = value.Replace('\\', '/');

        var matchesDirectoriesOnly = value.EndsWith('/');

        value = value.TrimEnd('/');

        if (value.Length == 0)
        {
            return null;
        }

        var isAnchored = value.Contains('/', StringComparison.Ordinal);

        if (value.StartsWith('/'))
        {
            value = value[1..];
            isAnchored = true;
        }
        else if (value.StartsWith("**/", StringComparison.Ordinal))
        {
            value = value[3..];
            isAnchored = value.Contains('/', StringComparison.Ordinal);
        }

        if (value.Length == 0)
        {
            return null;
        }

        var prefix = isAnchored ? "^" : "^(?:.*/)?";

        try
        {
            var regex = new Regex(
                prefix + Translate(value) + "$",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
                MatchTimeout);

            return new IgnorePattern(regex, isNegated, matchesDirectoriesOnly);
        }
        catch (ArgumentException)
        {
            // A malformed pattern is skipped rather than failing the whole ignore file
            return null;
        }
    }

    /// <summary>
    /// Tests the pattern against a path relative to the directory containing the ignore file.
    /// </summary>
    /// <param name="relativePath">Forward slash separated path, without leading or trailing separator.</param>
    /// <param name="isDirectory">Whether the item is a directory.</param>
    public bool Matches(string relativePath, bool isDirectory)
    {
        if (MatchesDirectoriesOnly && !isDirectory)
        {
            return false;
        }

        try
        {
            return _regex.IsMatch(relativePath);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static string Translate(string pattern)
    {
        var builder = new StringBuilder(pattern.Length * 2);
        var index = 0;

        while (index < pattern.Length)
        {
            var character = pattern[index];

            switch (character)
            {
                case '*':
                    if (index + 1 < pattern.Length && pattern[index + 1] == '*')
                    {
                        if (index + 2 < pattern.Length && pattern[index + 2] == '/')
                        {
                            // "**/" matches zero or more directories
                            builder.Append("(?:.*/)?");
                            index += 3;
                        }
                        else
                        {
                            builder.Append(".*");
                            index += 2;
                        }
                    }
                    else
                    {
                        builder.Append("[^/]*");
                        index++;
                    }

                    break;

                case '?':
                    builder.Append("[^/]");
                    index++;
                    break;

                case '[':
                    var closingIndex = pattern.IndexOf(']', index + 1);

                    if (closingIndex < 0)
                    {
                        builder.Append("\\[");
                        index++;
                        break;
                    }

                    var set = pattern[(index + 1)..closingIndex];

                    if (set.StartsWith('!'))
                    {
                        set = "^" + set[1..];
                    }

                    builder.Append('[').Append(set).Append(']');
                    index = closingIndex + 1;
                    break;

                default:
                    builder.Append(Regex.Escape(character.ToString()));
                    index++;
                    break;
            }
        }

        return builder.ToString();
    }

    private static string TrimTrailingUnescapedSpaces(string value)
    {
        var end = value.Length;

        while (end > 0 && value[end - 1] == ' ')
        {
            // A space preceded by an odd number of backslashes is escaped and kept
            var backslashes = 0;
            var index = end - 2;

            while (index >= 0 && value[index] == '\\')
            {
                backslashes++;
                index--;
            }

            if (backslashes % 2 == 1)
            {
                break;
            }

            end--;
        }

        return value[..end];
    }
}
