using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Volumes.Contracts;

internal sealed class VolumeCreationParameters
{
    [JsonPropertyName("AddressID")]
    public required string AddressId { get; init; }

    [JsonPropertyName("AddressKeyID")]
    public required string AddressKeyId { get; init; }

    public required string ShareKey { get; init; }
    public required string SharePassphrase { get; init; }
    public required string SharePassphraseSignature { get; init; }
    public required string FolderName { get; init; }
    public required string FolderKey { get; init; }
    public required string FolderPassphrase { get; init; }
    public required string FolderPassphraseSignature { get; init; }
    public required string FolderHashKey { get; init; }
}
