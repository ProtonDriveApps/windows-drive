using System.Collections.Immutable;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

internal sealed record AddressListResponse : ApiResponse
{
    private IImmutableList<AddressDto>? _addresses;

    public IImmutableList<AddressDto> Addresses
    {
        get => _addresses ??= ImmutableList<AddressDto>.Empty;
        init => _addresses = value;
    }
}
