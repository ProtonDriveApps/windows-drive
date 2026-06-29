namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Local.SpecialFolders;

internal interface ILocalTrash<TId> : ISpecialFolder<TId>
    where TId : IEquatable<TId>
{
    void StartAutomaticDisposal();
    Task StopAutomaticDisposalAsync();
    Task Empty();
}
