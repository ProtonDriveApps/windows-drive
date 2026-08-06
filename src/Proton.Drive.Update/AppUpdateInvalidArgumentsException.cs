namespace Proton.Drive.Update;

public sealed class AppUpdateInvalidArgumentsException : AppUpdateException
{
    public AppUpdateInvalidArgumentsException(string message)
        : base(message)
    {
    }

    public AppUpdateInvalidArgumentsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public AppUpdateInvalidArgumentsException()
    {
    }
}
