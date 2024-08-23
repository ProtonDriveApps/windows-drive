using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal class SafeFileStream : MappingExceptionsStream
{
    private readonly long _id;

    public SafeFileStream(Stream origin, long id)
        : base(origin)
    {
        _id = id;
    }

    protected override bool TryMapException(Exception exception, [MaybeNullWhen(false)] out Exception mappedException)
    {
        mappedException = exception switch
        {
            IOException ex => FileSystemClientException(ex),
            UnauthorizedAccessException ex => FileSystemClientException(ex),
            _ => null,
        };

        return mappedException is not null;
    }

    private Exception FileSystemClientException(Exception innerException)
    {
        return new FileSystemClientException<long>(
            FileSystemErrorCode.Unknown,
            objectId: default,
            innerException)
        {
            IsInnerExceptionMessageAuthoritative = true,
        };
    }
}
