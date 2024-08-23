using System;

namespace ProtonDrive.Sync.Shared.Trees;

public class TreeException : Exception
{
    public TreeException()
    {
    }

    public TreeException(string message)
        : base(message)
    {
    }

    public TreeException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
