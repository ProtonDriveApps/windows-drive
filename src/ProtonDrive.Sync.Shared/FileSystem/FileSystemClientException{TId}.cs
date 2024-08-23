using System;

namespace ProtonDrive.Sync.Shared.FileSystem;

public class FileSystemClientException<TId> : FileSystemClientException
{
    public TId? ObjectId { get; }

    public FileSystemClientException()
    {
    }

    public FileSystemClientException(string message)
        : base(message)
    {
    }

    public FileSystemClientException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    public FileSystemClientException(FileSystemErrorCode errorCode, TId? objectId, Exception? innerException)
        : this($"{errorCode}", errorCode, objectId, innerException)
    {
    }

    public FileSystemClientException(string message, FileSystemErrorCode errorCode, TId? objectId)
        : this(message, errorCode, objectId, innerException: default)
    {
    }

    public FileSystemClientException(string message, FileSystemErrorCode errorCode, TId? objectId, Exception? innerException)
        : base(message, errorCode, innerException)
    {
        ObjectId = objectId;
    }
}
