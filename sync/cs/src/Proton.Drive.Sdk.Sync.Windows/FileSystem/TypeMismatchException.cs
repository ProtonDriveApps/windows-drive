namespace Proton.Drive.Sdk.Sync.Windows.FileSystem;

public class TypeMismatchException : IOException
{
    public TypeMismatchException(string message)
        : base(message)
    {
    }

    public TypeMismatchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public TypeMismatchException()
    {
    }
}
