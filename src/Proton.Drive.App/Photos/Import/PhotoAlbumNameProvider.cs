using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Metadata.GoogleTakeout;
using Proton.Drive.Shared.IO;
using Proton.Drive.Shared.Logging;

namespace Proton.Drive.App.Photos.Import;

internal sealed class PhotoAlbumNameProvider : IPhotoAlbumNameProvider
{
    private const char NameSeparatorCharacter = ' ';
    private const char NameSuffixCharacter = '~';

    private readonly ILogger<PhotoAlbumNameProvider> _logger;

    public PhotoAlbumNameProvider(ILogger<PhotoAlbumNameProvider> logger)
    {
        _logger = logger;
    }

    public string GetAlbumNameFromPath(string folderPath, ReadOnlySpan<char> rootFolderPath, ReadOnlySpan<char> relativeFolderPath)
    {
        if (TryGetGoogleTakeoutAlbumNameFromMetadata(folderPath, out var albumName))
        {
            return albumName;
        }

        if (GoogleTakeoutPaths.IsFromGoogleTakeoutImport(folderPath))
        {
            return Path.GetFileName(folderPath);
        }

        if (relativeFolderPath.IsEmpty)
        {
            return GetDisplayName(rootFolderPath).ToString();
        }

        var rootFolderName = GetDisplayName(rootFolderPath);

        return GetAlbumName(rootFolderName, relativeFolderPath);
    }

    private static string GetAlbumName(ReadOnlySpan<char> rootFolderName, ReadOnlySpan<char> relativePath)
    {
        return string.Create(
            rootFolderName.Length + 1 + relativePath.Length,
            new AlbumNameSegments(rootFolderName, relativePath),
            (result, segments) =>
            {
                segments.RootName.CopyTo(result);
                result[segments.RootName.Length] = NameSeparatorCharacter;
                segments.RelativePath.CopyTo(result[(segments.RootName.Length + 1)..]);
                result.Replace(Path.DirectorySeparatorChar, NameSeparatorCharacter);
            });
    }

    private static ReadOnlySpan<char> GetDisplayName(ReadOnlySpan<char> path)
    {
        var displayName = PathExtensions.GetDisplayNameWithoutAccess(path);

        if (displayName.EndsWith(Path.VolumeSeparatorChar))
        {
            displayName = displayName[..^1];
        }

        if (displayName.IsEmpty)
        {
            displayName = [NameSuffixCharacter];
        }

        return displayName;
    }

    private bool TryGetGoogleTakeoutAlbumNameFromMetadata(string albumFolderPath, [MaybeNullWhen(false)] out string albumName)
    {
        try
        {
            const string metadataFileName = "metadata.json";

            var metadataJsonFilePath = Path.Combine(albumFolderPath, metadataFileName);

            if (!File.Exists(metadataJsonFilePath))
            {
                albumName = null;
                return false;
            }

            using var fileStream = File.Open(metadataJsonFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Size guard to avoid unnecessary operations and memory pressure
            if (fileStream.Length > 1024)
            {
                albumName = null;
                return false;
            }

            var albumMetadata = JsonSerializer.Deserialize<GoogleTakeoutAlbumMetadata>(fileStream);

            if (string.IsNullOrEmpty(albumMetadata?.AlbumName))
            {
                albumName = null;
                return false;
            }

            albumName = albumMetadata.AlbumName;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Failed to read Google Takeout album metadata from \"{Path}\"", _logger.GetSensitiveValueForLogging(albumFolderPath));
            albumName = null;
            return false;
        }
    }

    private readonly ref struct AlbumNameSegments(ReadOnlySpan<char> rootName, ReadOnlySpan<char> relativePath)
    {
        public ReadOnlySpan<char> RootName { get; } = rootName;
        public ReadOnlySpan<char> RelativePath { get; } = relativePath;
    }

    private sealed class GoogleTakeoutAlbumMetadata
    {
        [JsonPropertyName("title")]
        public string? AlbumName { get; init; }
    }
}
