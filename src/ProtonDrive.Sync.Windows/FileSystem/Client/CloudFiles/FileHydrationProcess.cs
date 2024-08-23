using System;
using System.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Windows.FileSystem.Client.CloudFiles;

internal sealed class FileHydrationProcess<TId> : IFileHydrationDemand<TId>, IDisposable
    where TId : IEquatable<TId>
{
    private readonly CloudFilesDataTransferStream _stream;
    private readonly Func<long, NodeInfo<TId>> _updateFileSize;

    public FileHydrationProcess(NodeInfo<TId> fileInfo, CloudFilesDataTransferStream stream, Func<long, NodeInfo<TId>> updateFileSize)
    {
        _stream = stream;
        _updateFileSize = updateFileSize;
        FileInfo = fileInfo;
    }

    public NodeInfo<TId> FileInfo { get; }

    public Stream HydrationStream => _stream;

    public NodeInfo<TId> UpdateFileSize()
    {
        return _updateFileSize.Invoke(_stream.Length);
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}
