using System.Diagnostics.CodeAnalysis;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Metadata.LivePhoto;

/// <summary>
/// Detects and manages Live Photo file relationships captured on iOS devices.
/// Live Photos consist of a main photo file (.heic, .jpeg, .jpg) paired with a video file (.mp4, .mov)
/// that share the same base filename and are located in the same directory.
/// </summary>
public sealed class LivePhotoFileDetector : ILivePhotoFileDetector
{
    /// <summary>
    /// Set of supported photo file extensions that may represent the main photo component of a "Live Photo" captured on iOS devices.
    /// </summary>
    private readonly HashSet<string> _livePhotoMainExtensions = new([".heic", ".jpeg", ".jpg"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Set of supported video file extensions that may represent the video component of a "Live Photo" captured on iOS devices.
    /// </summary>
    private readonly HashSet<string> _livePhotoVideoExtensions = new([".mp4", ".mov"], StringComparer.OrdinalIgnoreCase);

    public bool TryGetMainLivePhotoPath(string videoLivePhotoPath, [MaybeNullWhen(false)] out string mainLivePhotoPath)
    {
        mainLivePhotoPath = null;
        if (!_livePhotoVideoExtensions.Contains(Path.GetExtension(videoLivePhotoPath)))
        {
            return false;
        }

        var parentFolderPath = Path.GetDirectoryName(videoLivePhotoPath);
        if (parentFolderPath is null)
        {
            return false;
        }

        var relatedPhotoFileName = Path.GetFileNameWithoutExtension(videoLivePhotoPath);
        foreach (string extension in _livePhotoMainExtensions)
        {
            mainLivePhotoPath = Path.Combine(parentFolderPath, relatedPhotoFileName + extension);
            if (!File.Exists(mainLivePhotoPath))
            {
                continue;
            }

            return true;
        }

        mainLivePhotoPath = null;
        return false;
    }

    public bool IsVideoRelatedToLivePhoto(string videoLivePhotoPath, string mainLivePhotoPath)
    {
        if (string.IsNullOrEmpty(videoLivePhotoPath) || string.IsNullOrEmpty(mainLivePhotoPath))
        {
            return false;
        }

        if (!_livePhotoVideoExtensions.Contains(Path.GetExtension(videoLivePhotoPath)))
        {
            return false;
        }

        if (!_livePhotoMainExtensions.Contains(Path.GetExtension(mainLivePhotoPath)))
        {
            return false;
        }

        ReadOnlySpan<char> filePathSpan = videoLivePhotoPath.AsSpan();
        ReadOnlySpan<char> mainPhotoPathSpan = mainLivePhotoPath.AsSpan();

        ReadOnlySpan<char> fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePathSpan);
        ReadOnlySpan<char> mainPhotoNameWithoutExtension = Path.GetFileNameWithoutExtension(mainPhotoPathSpan);

        return fileNameWithoutExtension.Equals(mainPhotoNameWithoutExtension, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsLivePhoto(string filePath)
    {
        if (!_livePhotoMainExtensions.Contains(Path.GetExtension(filePath)))
        {
            return false;
        }

        var parentFolderPath = Path.GetDirectoryName(filePath);
        if (parentFolderPath is null)
        {
            return false;
        }

        var fileName = Path.GetFileNameWithoutExtension(filePath);

        return _livePhotoVideoExtensions
            .Select(videoExtension => Path.Combine(parentFolderPath, fileName + videoExtension))
            .Any(File.Exists);
    }
}
