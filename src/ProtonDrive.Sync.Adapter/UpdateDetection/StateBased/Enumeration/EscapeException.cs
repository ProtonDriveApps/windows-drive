using System;

namespace ProtonDrive.Sync.Adapter.UpdateDetection.StateBased.Enumeration;

internal class EscapeException : Exception
{
    public EscapeException()
    {
    }

    public EscapeException(string message)
        : base(message)
    {
    }

    public EscapeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
