using System.Collections.Immutable;

namespace ProtonDrive.Client.Contracts;

internal sealed record AddressListResponse : ApiResponse
{
    private IImmutableList<AddressDto>? _addresses;

    public IImmutableList<AddressDto> Addresses
    {
        get => _addresses ??= ImmutableList<AddressDto>.Empty;
        init => _addresses = value;
    }
}
