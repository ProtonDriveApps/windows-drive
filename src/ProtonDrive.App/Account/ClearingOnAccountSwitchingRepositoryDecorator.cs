using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared.Repository;

namespace ProtonDrive.App.Account;

internal sealed class ClearingOnAccountSwitchingRepositoryDecorator<T> : IRepository<T>, IAccountSwitchingHandler
{
    private readonly IRepository<T> _decoratedInstance;

    public ClearingOnAccountSwitchingRepositoryDecorator(IRepository<T> instanceToDecorate)
    {
        _decoratedInstance = instanceToDecorate;
    }

    public T? Get()
    {
        return _decoratedInstance.Get();
    }

    public void Set(T? value)
    {
        _decoratedInstance.Set(value);
    }

    Task<bool> IAccountSwitchingHandler.HandleAccountSwitchingAsync(CancellationToken cancellationToken)
    {
        _decoratedInstance.Set(default);

        return Task.FromResult(true);
    }
}
