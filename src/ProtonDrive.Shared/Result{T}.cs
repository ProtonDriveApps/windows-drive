using System;

namespace ProtonDrive.Shared;

public class Result<T> : Result
{
    protected internal Result(T? value, bool isSuccess, string? errorMessage, Exception? exception)
        : base(isSuccess, errorMessage, exception)
    {
        Value = value;
    }

    public T? Value { get; }
}
