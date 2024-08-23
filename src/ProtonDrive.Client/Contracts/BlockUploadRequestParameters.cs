using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using ProtonDrive.Shared.Text.Serialization;

namespace ProtonDrive.Client.Contracts;

internal sealed class BlockUploadRequestParameters
{
    [JsonPropertyName("BlockList")]
    public IReadOnlyCollection<BlockCreationParameters>? Blocks { get; init; }

    [JsonPropertyName("AddressID")]
    public string? AddressId { get; init; }

    [JsonPropertyName("ShareID")]
    public string? ShareId { get; init; }

    [JsonPropertyName("LinkID")]
    public string? LinkId { get; init; }

    [JsonPropertyName("RevisionID")]
    public string? RevisionId { get; init; }

    [JsonPropertyName("Thumbnail")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool? IncludesThumbnail { get; set; }

    [JsonConverter(typeof(Base64JsonConverter))]
    public ReadOnlyMemory<byte>? ThumbnailHash { get; set; }

    public int? ThumbnailSize { get; set; }
}
