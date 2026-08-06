using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Metadata.GoogleTakeout;

internal sealed class GoogleTakeoutMetadataContract
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("photoTakenTime")]
    public GoogleTakeoutDateTimeContract? TakenTime { get; init; }

    [JsonPropertyName("geoData")]
    public GoogleTakeoutGeoDataContract? GeoData { get; init; }

    [JsonPropertyName("geoDataExif")]
    public GoogleTakeoutGeoDataContract? GeoDataExif { get; init; }
}
