using System.Text.RegularExpressions;

namespace ProtonDrive.App.FileSystem.Metadata.GoogleTakeout;

internal static partial class GoogleTakeoutMetadataFileNameGenerator
{
    internal const int GoogleTakeoutFileNameMaxLength = 45;

    private const string MetadataFileExtension = ".supplemental-metadata";
    private const string JsonExtension = ".json";
    private const int MaxFileNameLength = 255;

    public static IEnumerable<string> GetFileNameCandidates(string fileName)
    {
        // Google Takeout cuts photo file names to much less than 255 characters.
        // If the name is longer, it does not belong to Google Takeout.
        if (fileName.Length + MetadataFileExtension.Length + JsonExtension.Length > MaxFileNameLength)
        {
            yield break;
        }

        yield return fileName + MetadataFileExtension + JsonExtension;

        for (var i = 1; i <= MetadataFileExtension.Length; i++)
        {
            yield return fileName + MetadataFileExtension[..^i] + JsonExtension;
        }

        if (fileName.Length > GoogleTakeoutFileNameMaxLength)
        {
            for (var i = 1; i < fileName.Length - GoogleTakeoutFileNameMaxLength; i++)
            {
                yield return fileName[..^i] + JsonExtension;
            }
        }

        // When Google Takeout exports duplicate-named photos, it appends an index to the file name ("Photo(1).jpg")
        // but moves that index to after the extension in the metadata file name ("Photo.jpg.supplemental-metadata(1).json").
        foreach (var candidate in GetTrailingSuffixFileNameCandidates(fileName))
        {
            yield return candidate;
        }
    }

    [GeneratedRegex(@"\((\d+)\)$")]
    private static partial Regex TrailingSuffixRegex();

    private static IEnumerable<string> GetTrailingSuffixFileNameCandidates(string fileName)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var fileExtension = Path.GetExtension(fileName);
        var match = TrailingSuffixRegex().Match(fileNameWithoutExtension);

        if (!match.Success)
        {
            yield break;
        }

        var indexSuffix = match.Value;
        var metadataFileNamePrefix = fileNameWithoutExtension[..match.Index] + fileExtension + MetadataFileExtension;

        yield return metadataFileNamePrefix + indexSuffix + JsonExtension;

        if (metadataFileNamePrefix.Length > GoogleTakeoutFileNameMaxLength)
        {
            for (var i = 1; i < metadataFileNamePrefix.Length - GoogleTakeoutFileNameMaxLength; i++)
            {
                yield return metadataFileNamePrefix[..^i] + indexSuffix + JsonExtension;
            }
        }
    }
}
