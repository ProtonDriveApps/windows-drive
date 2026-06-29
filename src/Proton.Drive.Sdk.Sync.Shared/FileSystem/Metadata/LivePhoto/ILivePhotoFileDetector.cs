using System.Diagnostics.CodeAnalysis;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Metadata.LivePhoto;

/// <summary>
/// Provides functionality to detect and manage Live Photo file relationships captured on iOS devices.
/// Live Photos consist of a main photo file (.heic, .jpeg, .jpg) paired with a video file (.mp4, .mov)
/// that share the same base filename and are located in the same directory.
/// </summary>
public interface ILivePhotoFileDetector
{
    /// <summary>
    /// Attempts to find the main photo component of a "Live Photo" corresponding to the given video file.
    /// </summary>
    /// <param name="videoLivePhotoPath">The full path to the video file to check.</param>
    /// <param name="mainLivePhotoPath">
    /// When the method returns <c>true</c>, contains the path to the matching photo file (.heic, .jpeg, or .jpg).
    /// Otherwise, the value is <c>null</c>.
    /// </param>
    /// <returns>
    /// <c>true</c> if a related Live Photo image file was found; otherwise, <c>false</c>.
    /// </returns>
    bool TryGetMainLivePhotoPath(string videoLivePhotoPath, [MaybeNullWhen(false)] out string mainLivePhotoPath);

    /// <summary>
    /// Determines if a video file is related to a photo file as part of a "Live Photo" pair.
    /// Both files must have the same base filename (without extension) and be located in the same folder.
    /// </summary>
    /// <param name="videoLivePhotoPath">The full path to the video file to check.</param>
    /// <param name="mainLivePhotoPath">The full path to the photo file to check against.</param>
    /// <returns>
    /// <c>true</c> if the video and photo files form a Live Photo pair; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// A Live Photo pair consists of a photo file (.heic, .jpeg, or .jpg) and a video file (.mp4 or .mov)
    /// with identical filenames (excluding extensions) in the same directory.
    /// </remarks>
    bool IsVideoRelatedToLivePhoto(string videoLivePhotoPath, string mainLivePhotoPath);

    /// <summary>
    /// Indicate if the file is a Live Photo.
    /// </summary>
    /// <param name="filePath">The full path of the file to check.</param>
    /// <remarks>
    /// A Live Photo pair consists of a photo file (.heic, .jpeg, or .jpg) and a video file (.mp4 or .mov)
    /// with identical filenames (excluding extensions) in the same directory.
    /// </remarks>
    /// <returns>
    /// <c>true</c> if the file is a Live Photo; otherwise, <c>false</c>
    /// </returns>
    bool IsLivePhoto(string filePath);
}
