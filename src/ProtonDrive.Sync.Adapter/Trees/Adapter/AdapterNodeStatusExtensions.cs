namespace ProtonDrive.Sync.Adapter.Trees.Adapter;

internal static class AdapterNodeStatusExtensions
{
    public static bool HasAnyFlag(this AdapterNodeStatus item, AdapterNodeStatus value)
    {
        return (item & value) != AdapterNodeStatus.None;
    }
}
