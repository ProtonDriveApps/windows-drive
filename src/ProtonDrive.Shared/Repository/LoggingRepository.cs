using System;
using System.Collections;
using System.Linq;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Logging;

namespace ProtonDrive.Shared.Repository;

public class LoggingRepository<T> : IRepository<T>, IThrowsExpectedExceptions
{
    private readonly ILogger _logger;
    private readonly IRepository<T> _origin;

    public LoggingRepository(ILogger<LoggingRepository<T>> logger, IRepository<T> origin)
    {
        Ensure.IsTrue(origin is IThrowsExpectedExceptions, $"{nameof(origin)} must implement {nameof(IThrowsExpectedExceptions)} interface");

        _logger = logger;
        _origin = origin;
    }

    public T? Get()
    {
        return _logger.WithLoggedException(
            () => _origin.Get(),
            () => $"Failed reading {NameOf(typeof(T))} from storage");
    }

    public void Set(T? value)
    {
        _logger.WithLoggedException(
            () => _origin.Set(value),
            () => $"Failed writing {NameOf(typeof(T))} to storage");
    }

    public bool IsExpectedException(Exception ex)
    {
        return ex.IsExpectedExceptionOf(_origin);
    }

    private static string NameOf(Type type)
    {
        if (IsEnumerableType(type) && type.GetGenericArguments().Any())
        {
            return $"{type.GetGenericArguments()[0].Name} collection";
        }

        return type.Name;
    }

    private static bool IsEnumerableType(Type type)
    {
        return typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);
    }
}
