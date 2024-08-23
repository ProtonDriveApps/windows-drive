using System;

namespace ProtonDrive.App.Reporting;

public sealed class IntentionalCrashException : Exception
{
    public IntentionalCrashException()
        : this("Intentional crash test")
    {
    }

    public IntentionalCrashException(string message)
        : base(message)
    {
    }

    public IntentionalCrashException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
