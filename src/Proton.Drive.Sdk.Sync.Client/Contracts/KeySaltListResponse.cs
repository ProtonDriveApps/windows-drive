using System.Collections.Immutable;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record KeySaltListResponse : ApiResponse
{
    private IImmutableList<KeySalt>? _keySalts;

    public IImmutableList<KeySalt> KeySalts
    {
        get => _keySalts ??= ImmutableList<KeySalt>.Empty;
        init => _keySalts = value;
    }
}
