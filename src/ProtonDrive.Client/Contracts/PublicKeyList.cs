using System.Collections.Immutable;

namespace ProtonDrive.Client.Contracts;

public sealed record PublicKeyList
{
    private IImmutableList<PublicKeyEntry>? _keys;

    public IImmutableList<PublicKeyEntry> Keys
    {
        get => _keys ??= ImmutableList<PublicKeyEntry>.Empty;
        init => _keys = value;
    }
}
