using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using ProtonDrive.Shared.IO;

namespace ProtonDrive.Client;

internal class SafeRemoteFileStream : MappingExceptionsStream
{
    private readonly string? _id;

    public SafeRemoteFileStream(Stream instanceToDecorate, string? id)
        : base(instanceToDecorate)
    {
        _id = id;
    }

    protected override bool TryMapException(Exception exception, [MaybeNullWhen(false)] out Exception mappedException)
    {
        if (!ExceptionMapping.TryMapException(exception, _id, includeObjectId: false, out var fileSystemClientException))
        {
            mappedException = null;
            return false;
        }

        mappedException = fileSystemClientException;

        return true;
    }
}
