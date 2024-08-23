using System.Collections.Immutable;

namespace ProtonDrive.Client.Contracts;

public sealed record KeySaltListResponse : ApiResponse
{
    private IImmutableList<KeySalt>? _keySalts;

    public IImmutableList<KeySalt> KeySalts
    {
        get => _keySalts ??= ImmutableList<KeySalt>.Empty;
        init => _keySalts = value;
    }
}
