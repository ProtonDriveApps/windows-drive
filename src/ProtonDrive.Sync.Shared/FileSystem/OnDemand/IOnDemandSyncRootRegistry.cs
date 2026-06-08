namespace ProtonDrive.Sync.Shared.FileSystem.OnDemand;

public interface IOnDemandSyncRootRegistry
{
    /// <summary>
    /// Verifies whether the provided path belongs to on-demand sync root with expected characteristics
    /// </summary>
    /// <param name="root">Sync root and shell folder information</param>
    /// <returns>A <see cref="OnDemandSyncRootVerificationResult"/>.</returns>
    Task<OnDemandSyncRootVerificationResult> VerifyAsync(OnDemandSyncRootInfo root);

    /// <summary>
    /// Registers on-demand sync root and adds shell folder
    /// </summary>
    /// <param name="root">Sync root and shell folder information</param>
    /// <returns>True if succeeded; False otherwise.</returns>
    Task<bool> TryRegisterAsync(OnDemandSyncRootInfo root);

    /// <summary>
    /// Removes on-demand shell folder and un-registers sync root
    /// </summary>
    /// <param name="root">Sync root and shell folder information</param>
    /// <returns>True if succeeded; False otherwise.</returns>
    Task<bool> TryUnregisterAsync(OnDemandSyncRootInfo root);

    /// <summary>
    /// Attempts to remove all on-demand shell folders and un-register sync roots
    /// </summary>
    /// <returns>True if succeeded; False otherwise.</returns>
    bool TryUnregisterAll();
}
