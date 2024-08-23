using System;
using ProtonDrive.Shared.Repository;

namespace ProtonDrive.Sync.Shared.Property;

public class TransactedCachingRepository<T> : IRepository<T>
{
    private readonly IRepository<T> _origin;

    private bool _initialized;
    private bool _updated;
    private T? _value;

    public TransactedCachingRepository(ITransactionProvider transactionProvider, IRepository<T> origin)
    {
        _origin = origin;

        transactionProvider.TransactionEnds += TransactionProvider_TransactionEnds;
    }

    public T? Get()
    {
        if (!_initialized)
        {
            _value = _origin.Get();
            _initialized = true;
            _updated = false;
        }

        return _value;
    }

    public void Set(T? value)
    {
        _value = value;
        _initialized = true;
        _updated = true;
    }

    private void TransactionProvider_TransactionEnds(object? sender, EventArgs e)
    {
        if (_updated)
        {
            _origin.Set(_value);
            _updated = false;
        }
    }
}
