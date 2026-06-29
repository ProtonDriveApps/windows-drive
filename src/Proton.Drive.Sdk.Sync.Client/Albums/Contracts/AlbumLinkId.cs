using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Albums.Contracts;

public sealed record AlbumLinkId([property: JsonPropertyName("LinkID")] string Value);
