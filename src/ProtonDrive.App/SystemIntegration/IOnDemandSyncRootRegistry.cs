using System.Threading.Tasks;

namespace ProtonDrive.App.SystemIntegration;

public interface IOnDemandSyncRootRegistry
{
    /// <summary>
    /// Registers on-demand sync root and adds shell folder
    /// </summary>
    /// <param name="root">Sync root and shell folder information</param>
    /// <returns>True if succeeded; False otherwise.</returns>
    public Task<bool> TryRegisterAsync(OnDemandSyncRootInfo root);

    /// <summary>
    /// Removes on-demand shell folder and un-registers sync root
    /// </summary>
    /// <param name="root">Sync root and shell folder information</param>
    /// <returns>True if succeeded; False otherwise.</returns>
    public Task<bool> TryUnregisterAsync(OnDemandSyncRootInfo root);

    /// <summary>
    /// Attempts to remove all on-demand shell folders and un-register sync roots
    /// </summary>
    /// <returns>True if succeeded; False otherwise.</returns>
    public bool TryUnregisterAll();
}
