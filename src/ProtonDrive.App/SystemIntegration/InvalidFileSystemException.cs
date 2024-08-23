using System;

namespace ProtonDrive.Shared.Volume;

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
