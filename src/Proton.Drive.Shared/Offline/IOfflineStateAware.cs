namespace Proton.Drive.Shared.Offline;

public interface IOfflineStateAware
{
    void OnOfflineStateChanged(OfflineStatus status);
}
