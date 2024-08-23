using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed class VolumeCreationParameters
{
    [JsonPropertyName("AddressID")]
    public string? AddressId { get; set; }
    public string? VolumeName { get; set; }
    public string? ShareName { get; set; }
    public string? ShareKey { get; set; }
    public string? SharePassphrase { get; set; }
    public string? SharePassphraseSignature { get; set; }
    public string? FolderName { get; set; }
    public string? FolderKey { get; set; }
    public string? FolderPassphrase { get; set; }
    public string? FolderPassphraseSignature { get; set; }
    public string? FolderHashKey { get; set; }
    public long? VolumeMaxSpace { get; set; }
}
