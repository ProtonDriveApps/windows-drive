using System;
using System.IO;

namespace ProtonDrive.Shared.IO;

public static class PathComparison
{
    private static readonly char[] SeparatorChars = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

    // This method does not interpret "\..\" in the paths
    public static bool IsAncestor(string potentialAncestor, string potentialDescendent)
    {
        if (potentialAncestor.Length >= potentialDescendent.Length)
        {
            return false;
        }

        var startIndex = 0;
        while (startIndex < potentialAncestor.Length)
        {
            var endIndex = potentialDescendent.IndexOfAny(SeparatorChars, startIndex);
            var length = endIndex >= 0 ? endIndex - startIndex : potentialDescendent.Length - startIndex;

            var comparisonResult = string.Compare(potentialDescendent, startIndex, potentialAncestor, startIndex, length, StringComparison.OrdinalIgnoreCase);
            if (comparisonResult != 0)
            {
                return false;
            }

            startIndex += length + 1;
        }

        return startIndex > 0 && startIndex < potentialDescendent.Length;
    }
}
