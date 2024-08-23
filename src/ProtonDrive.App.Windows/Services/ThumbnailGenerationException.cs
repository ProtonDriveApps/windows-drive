using System;

namespace ProtonDrive.App.Windows.Services;

public class ThumbnailGenerationException : Exception
{
    public ThumbnailGenerationException(string message)
        : base(message)
    {
    }

    public ThumbnailGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ThumbnailGenerationException()
    {
    }
}
