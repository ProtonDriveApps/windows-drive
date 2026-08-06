namespace Proton.Drive.Shared.IO;

public static class PathComparison
{
    private static readonly char[] SeparatorChars = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    // This method does not interpret "\..\" in the paths
    public static bool IsAncestor(string potentialAncestor, string potentialDescendant)
    {
        if (potentialAncestor.Length >= potentialDescendant.Length)
        {
            return false;
        }

        var startIndex = 0;
        while (startIndex < potentialAncestor.Length)
        {
            var endIndex = potentialDescendant.IndexOfAny(SeparatorChars, startIndex);
            var length = endIndex >= 0 ? endIndex - startIndex : potentialDescendant.Length - startIndex;

            var comparisonResult = string.Compare(potentialDescendant, startIndex, potentialAncestor, startIndex, length, StringComparison.OrdinalIgnoreCase);
            if (comparisonResult != 0)
            {
                return false;
            }

            startIndex += length + 1;
        }

        return startIndex > 0 && startIndex < potentialDescendant.Length;
    }

    public static string EnsureTrailingSeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
    }
}
