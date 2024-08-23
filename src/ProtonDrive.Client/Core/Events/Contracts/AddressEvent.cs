using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Core.Events.Contracts;

internal sealed class AddressEvent
{
    public CoreEventAction Action { get; init; }

    [JsonPropertyName("ID")]
    public string Id { get; init; } = string.Empty;
}
