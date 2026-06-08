namespace ProtonDrive.Sync.Shared.FileSystem.Thumbnails;

public class ThumbnailGenerationException : Exception
{
    public ThumbnailGenerationException(ThumbnailGenerationErrorCode errorCode)
    {
        ErrorCode = errorCode;
    }

    public ThumbnailGenerationException(ThumbnailGenerationErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public ThumbnailGenerationException(ThumbnailGenerationErrorCode errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }

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

    public ThumbnailGenerationErrorCode ErrorCode { get; }
}
