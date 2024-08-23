namespace ProtonDrive.Sync.Shared.Trees;

public readonly record struct LooseCompoundAltIdentity<T>
{
    public int VolumeId { get; init; }
    public T? ItemId { get; init; }

    public static implicit operator LooseCompoundAltIdentity<T>(T? itemId) => (default, itemId);

    public static implicit operator LooseCompoundAltIdentity<T>((int VolumeId, T? ItemId) altId)
    {
        if (altId.ItemId is null || altId.ItemId.Equals(default))
        {
            return default;
        }

        return new LooseCompoundAltIdentity<T>
        {
            VolumeId = altId.VolumeId,
            ItemId = altId.ItemId,
        };
    }

    public bool IsDefault()
    {
        return Equals(default);
    }

    public override string ToString()
    {
        return $"({VolumeId}/{ItemId})";
    }
}
