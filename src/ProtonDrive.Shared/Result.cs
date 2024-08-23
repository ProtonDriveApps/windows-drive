using System;

namespace ProtonDrive.Shared;

public class Result
{
    protected Result(bool isSuccess, string? errorMessage = null, Exception? exception = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public bool IsSuccess { get; }

    public string? ErrorMessage { get; }

    public Exception? Exception { get; }

    public bool IsFailure => !IsSuccess;

    public static Result Failure(string? errorMessage = null)
        => new(isSuccess: false, errorMessage);

    public static Result Failure(Exception exception)
        => new(false, errorMessage: null, exception);

    public static Result<T> Failure<T>(string? errorMessage = null)
        => new(default, isSuccess: false, errorMessage, exception: null);

    public static Result<T> Failure<T>(T? value, Exception? exception = null)
        => new(value, isSuccess: false, errorMessage: null, exception);

    public static Result Success()
        => new(isSuccess: true);

    public static Result<T> Success<T>(T? value = default)
        => new(value, isSuccess: true, errorMessage: null, exception: null);
}
