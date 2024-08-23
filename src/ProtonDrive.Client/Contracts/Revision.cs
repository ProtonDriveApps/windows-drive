using System.Collections.Immutable;

namespace ProtonDrive.Client.Contracts;

public sealed class Revision : RevisionHeader
{
    private IImmutableList<Block>? _blocks;

    public IImmutableList<Block> Blocks
    {
        get => _blocks ??= ImmutableList.Create<Block>();
        init => _blocks = value;
    }
}
