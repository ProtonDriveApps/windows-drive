namespace ProtonDrive.Shared.Offline;

public interface IOfflineStateAware
{
    void OnOfflineStateChanged(OfflineStatus status);
}
