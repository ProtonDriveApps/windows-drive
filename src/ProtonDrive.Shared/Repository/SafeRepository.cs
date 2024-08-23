using System;
using ProtonDrive.Shared.Extensions;

namespace ProtonDrive.Shared.Repository;

public class SafeRepository<T> : IRepository<T>
{
    private readonly IRepository<T> _origin;

    public SafeRepository(IRepository<T> origin)
    {
        Ensure.IsTrue(origin is IThrowsExpectedExceptions, $"{nameof(origin)} must implement {nameof(IThrowsExpectedExceptions)} interface");

        _origin = origin;
    }

    public T? Get()
    {
        try
        {
            return _origin.Get();
        }
        catch (Exception ex) when (ex.IsExpectedExceptionOf(_origin))
        {
            return default;
        }
    }

    public void Set(T? value)
    {
        try
        {
            _origin.Set(value);
        }
        catch (Exception ex) when (ex.IsExpectedExceptionOf(_origin))
        {
        }
    }
}
