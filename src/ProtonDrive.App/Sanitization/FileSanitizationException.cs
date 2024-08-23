using System;

namespace ProtonDrive.App.Sanitization;

internal sealed class FileSanitizationException : Exception
{
    public FileSanitizationException()
    {
    }

    public FileSanitizationException(string message)
        : base(message)
    {
    }

    public FileSanitizationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
