using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Core.Events.Contracts;

internal sealed class AddressEvent
{
    public CoreEventAction Action { get; init; }

    [JsonPropertyName("ID")]
    public string Id { get; init; } = string.Empty;
}
