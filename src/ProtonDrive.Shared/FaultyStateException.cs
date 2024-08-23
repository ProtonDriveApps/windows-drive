using System;

namespace ProtonDrive.Shared;

public class FaultyStateException : Exception
{
    public FaultyStateException()
        : this("Internal state is faulty")
    {
    }

    public FaultyStateException(string message)
        : base(message)
    {
    }

    public FaultyStateException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
