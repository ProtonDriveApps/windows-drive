using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record ShareResponse : ApiResponse
{
    private IImmutableList<ShareMembership>? _memberships;

    [JsonPropertyName("ShareID")]
    public string Id { get; init; } = string.Empty;

    public LinkType LinkType { get; init; }

    public ShareType Type { get; init; }

    public ShareState State { get; init; }

    [JsonPropertyName("LinkID")]
    public string LinkId { get; init; } = string.Empty;

    [JsonPropertyName("VolumeID")]
    public string VolumeId { get; init; } = string.Empty;

    [JsonPropertyName("Creator")]
    public string CreatorEmailAddress { get; init; } = string.Empty;

    [JsonPropertyName("Locked")]
    public bool IsLocked { get; init; }

    public IImmutableList<ShareMembership> Memberships
    {
        get => _memberships ??= ImmutableList<ShareMembership>.Empty;
        init => _memberships = value;
    }

    public string Key { get; init; } = string.Empty;
    public string Passphrase { get; init; } = string.Empty;
    public string PassphraseSignature { get; init; } = string.Empty;

    [JsonPropertyName("AddressID")]
    public string? AddressId { get; init; }

    /// <summary>
    /// The best guess of the backend about the identifier of the address key used for encrypting passphrase.
    /// The value can be wrong.
    /// </summary>
    [JsonPropertyName("AddressKeyID")]
    public string? AddressKeyId { get; init; }
}
