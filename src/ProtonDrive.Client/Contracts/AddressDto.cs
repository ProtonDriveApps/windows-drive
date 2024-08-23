using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

internal sealed record AddressDto
{
    private IImmutableList<AddressKeyDto>? _keys;

    [JsonPropertyName("ID")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("Email")]
    public string EmailAddress { get; init; } = string.Empty;

    public AddressStatus Status { get; init; }

    public int Order { get; init; }

    public IImmutableList<AddressKeyDto> Keys
    {
        get => _keys ??= ImmutableList<AddressKeyDto>.Empty;
        init => _keys = value;
    }
}
