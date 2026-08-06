using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Albums.Contracts;

public sealed record PhotoToAddParameter
{
    [JsonPropertyName("LinkID")]
    public required string LinkId { get; init; }

    [JsonPropertyName("Hash")]
    public required string NameHash { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// Email address used for signing name
    /// </summary>
    [JsonPropertyName("NameSignatureEmail")]
    public required string NameSignatureEmailAddress { get; init; }

    /// <summary>
    /// Armored PGP message
    /// </summary>
    public required string NodePassphrase { get; init; }

    public required string ContentHash { get; init; }
}
