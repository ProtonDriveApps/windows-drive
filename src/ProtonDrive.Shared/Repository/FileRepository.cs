using System;
using System.IO;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.Text.Serialization;

namespace ProtonDrive.Shared.Repository;

public class FileRepository<T> : IRepository<T>, IThrowsExpectedExceptions
{
    private readonly IBinarySerializer _serializer;
    private readonly string _fileName;

    public FileRepository(IBinarySerializer serializer, string fileName)
    {
        Ensure.IsTrue(serializer is IThrowsExpectedExceptions, $"{nameof(serializer)} must implement {nameof(IThrowsExpectedExceptions)} interface");

        _serializer = serializer;
        _fileName = fileName;
    }

    public T? Get()
    {
        if (!File.Exists(_fileName))
        {
            return default;
        }

        using var reader = new FileStream(_fileName, FileMode.Open, FileAccess.Read);

        return _serializer.Deserialize<T>(reader);
    }

    public void Set(T? value)
    {
        using var writer = new FileStream(_fileName, FileMode.Create, FileAccess.Write);

        _serializer.Serialize(value, writer);
    }

    public bool IsExpectedException(Exception ex)
    {
        return ex.IsFileAccessException() ||
               ex.IsExpectedExceptionOf(_serializer);
    }
}
