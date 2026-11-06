namespace Proton.Drive.Sdk.Sync.Shared.Ignore;

/// <summary>
/// The patterns of a single ignore file, bound to the directory that contains it.
/// </summary>
public sealed class IgnoreRuleSet
{
    public static readonly IgnoreRuleSet Empty = new(string.Empty, Array.Empty<IgnorePattern>());

    private readonly IReadOnlyList<IgnorePattern> _patterns;

    public IgnoreRuleSet(string basePath, IReadOnlyList<IgnorePattern> patterns)
    {
        BasePath = basePath;
        _patterns = patterns;
    }

    /// <summary>
    /// The directory containing the ignore file, relative to the sync root, forward slash separated.
    /// Empty for an ignore file located directly on the sync root.
    /// </summary>
    public string BasePath { get; }

    public bool IsEmpty => _patterns.Count == 0;

    public static IgnoreRuleSet Parse(string basePath, IEnumerable<string> lines)
    {
        var patterns = new List<IgnorePattern>();

        foreach (var line in lines)
        {
            var pattern = IgnorePattern.TryParse(line);

            if (pattern is not null)
            {
                patterns.Add(pattern);
            }
        }

        return patterns.Count == 0 && basePath.Length == 0
            ? Empty
            : new IgnoreRuleSet(basePath, patterns);
    }

    /// <summary>
    /// Tests all patterns against the given path. Later patterns override earlier ones, as in gitignore.
    /// </summary>
    /// <param name="relativePath">Forward slash separated path relative to the sync root.</param>
    /// <param name="isDirectory">Whether the item is a directory.</param>
    /// <returns>
    /// True if the item is excluded, false if it is explicitly re-included by a negated pattern,
    /// null if no pattern of this rule set applies.
    /// </returns>
    public bool? Match(string relativePath, bool isDirectory)
    {
        if (_patterns.Count == 0)
        {
            return null;
        }

        if (!TryGetScopedPath(relativePath, out var scopedPath))
        {
            return null;
        }

        bool? result = null;

        foreach (var pattern in _patterns)
        {
            if (pattern.Matches(scopedPath, isDirectory))
            {
                result = !pattern.IsNegated;
            }
        }

        return result;
    }

    private bool TryGetScopedPath(string relativePath, out string scopedPath)
    {
        if (BasePath.Length == 0)
        {
            scopedPath = relativePath;

            return true;
        }

        if (relativePath.Length <= BasePath.Length + 1 ||
            relativePath[BasePath.Length] != '/' ||
            !relativePath.AsSpan(0, BasePath.Length).Equals(BasePath, StringComparison.OrdinalIgnoreCase))
        {
            scopedPath = string.Empty;

            return false;
        }

        scopedPath = relativePath[(BasePath.Length + 1)..];

        return true;
    }
}
