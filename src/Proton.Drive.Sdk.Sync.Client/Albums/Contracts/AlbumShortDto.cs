using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Albums.Contracts;

public sealed record AlbumShortDto([property: JsonPropertyName("Link")] AlbumLinkId LinkId);
