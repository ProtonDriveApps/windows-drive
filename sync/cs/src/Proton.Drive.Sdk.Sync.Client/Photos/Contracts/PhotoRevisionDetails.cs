using System.Text.Json.Serialization;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Photos;

namespace Proton.Drive.Sdk.Sync.Client.Photos.Contracts;

/// <summary>
/// Photo specific details used during revision commit.
/// </summary>
/// <param name="CaptureTime">Represent a regular timezone-independent UNIX timestamp in seconds.
/// <remarks>
/// <para>It should be obtained from the EXIF data.</para>
/// <para>It should be the same time (in a different format and resolution) as the <code>Camera.CaptureTime</code> in the extended attributes,
/// with a fallback to the file creation time if there is no camera metadata.</para>
/// </remarks>
/// </param>
/// <param name="ContentHash">File content hash, lowercase hex representation of HMAC SHA256 of SHA1 content using parent folder's hash key as secret.</param>
/// <param name="MainPhotoLinkId">For related files, Link ID of the main photo.</param>
/// <param name="Tags">List of tags to be assigned to the photo.</param>
public sealed record PhotoRevisionDetails(
    long CaptureTime,
    string ContentHash,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)][property: JsonPropertyName("MainPhotoLinkID")] string? MainPhotoLinkId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlySet<PhotoTag>? Tags = null);
