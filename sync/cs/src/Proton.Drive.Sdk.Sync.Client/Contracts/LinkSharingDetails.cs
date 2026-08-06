using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed class LinkSharingDetails
{
    private readonly string? _shareId;

    [JsonPropertyName("ShareID")]
    public string ShareId
    {
        get => _shareId ?? throw new ArgumentNullException(nameof(ShareId));
        init => _shareId = value;
    }
}
