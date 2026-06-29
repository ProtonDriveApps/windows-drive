using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record SharedWithMeItem
{
    [JsonPropertyName("VolumeID")]
    public required string VolumeId { get; init; }

    [JsonPropertyName("ShareID")]
    public required string ShareId { get; init; }

    [JsonPropertyName("LinkID")]
    public required string LinkId { get; init; }

    /// <summary>
    /// The target type of the share that is corresponding to the share's invitation.
    /// This should not be used as source of information to know what node type the targeted share is.
    /// </summary>
    public ShareTargetType? ShareTargetType { get; set; }
}
