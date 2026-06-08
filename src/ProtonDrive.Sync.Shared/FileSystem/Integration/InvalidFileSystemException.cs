namespace ProtonDrive.Sync.Shared.FileSystem.Integration;

public class InvalidFileSystemException : Exception
{
    public InvalidFileSystemException()
    {
    }

    public InvalidFileSystemException(string message)
        : base(message)
    {
    }

    public InvalidFileSystemException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
