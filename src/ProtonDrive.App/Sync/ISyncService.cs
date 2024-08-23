using System.Threading.Tasks;
using ProtonDrive.Sync.Shared.SyncActivity;

namespace ProtonDrive.App.Sync;

public interface ISyncService
{
    SyncStatus Status { get; }
    bool Paused { get; set; }

    void Synchronize();
    Task RestartAsync();
}
