namespace ProtonDrive.Shared.Repository;

public class CachingRepository<T> : IProtectedRepository<T>
{
    private readonly IRepository<T> _origin;

    private bool _initialized;
    private T? _value;

    public CachingRepository(IRepository<T> origin)
    {
        _origin = origin;
    }

    public T? Get()
    {
        if (!_initialized)
        {
            _value = _origin.Get();
            _initialized = true;
        }

        return _value;
    }

    public void Set(T? value)
    {
        _value = value;
        _initialized = true;
        _origin.Set(value);
    }
}
