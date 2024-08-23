using System;
using System.Threading.Tasks;

namespace ProtonDrive.App.FileSystem.Local.SpecialFolders;

internal interface ILocalTrash<TId> : ISpecialFolder<TId>
    where TId : IEquatable<TId>
{
    void StartAutomaticDisposal();
    Task StopAutomaticDisposalAsync();
    Task Empty();
}
