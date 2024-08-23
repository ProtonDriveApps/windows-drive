using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Devices.Contracts;

internal sealed class DeviceShareCreationParameters
{
    [JsonPropertyName("AddressID")]
    public string? AddressId { get; set; }

    /// <summary>
    /// Device name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public string? Key { get; set; }
    public string? Passphrase { get; set; }
    public string? PassphraseSignature { get; set; }
}
