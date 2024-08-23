using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Volumes;

internal sealed class ThumbnailQueryParameters
{
    private IEnumerable<string>? _thumbnailIds;

    [JsonPropertyName("ThumbnailIDs")]
    public IEnumerable<string> ThumbnailIds
    {
        get => _thumbnailIds ??= Array.Empty<string>();
        init => _thumbnailIds = value;
    }
}
