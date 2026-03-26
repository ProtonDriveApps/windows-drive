using ProtonDrive.Shared.Extensions;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Windows.FileSystem.Client.CloudFiles;

internal sealed class FileHydrationProcess<TId> : IFileHydrationDemand<TId>, IDisposable
    where TId : IEquatable<TId>
{
    private readonly CloudFilesDataTransferStream _stream;
    private readonly Func<long, NodeInfo<TId>> _updateFileSize;

    private ChecksumVerifyingStream? _verifyingStream;

    public FileHydrationProcess(NodeInfo<TId> fileInfo, bool checksumVerificationEnabled, CloudFilesDataTransferStream stream, Func<long, NodeInfo<TId>> updateFileSize)
    {
        FileInfo = fileInfo;
        ChecksumVerificationEnabled = checksumVerificationEnabled;
        _stream = stream;
        _updateFileSize = updateFileSize;
    }

    public NodeInfo<TId> FileInfo { get; private set; }
    public bool ChecksumVerificationEnabled { get; }
    public bool ChecksumVerificationPerformed => _verifyingStream is not null;
    public bool ChecksumVerificationFailed => _verifyingStream?.VerificationFailed == true;

    public Stream GetHydrationStream(ReadOnlyMemory<byte>? expectedSha1)
    {
        if (expectedSha1 is null)
        {
            return _stream;
        }

        _verifyingStream = new ChecksumVerifyingStream(_stream, expectedSha1.Value);

        return _verifyingStream;
    }

    public NodeInfo<TId> UpdateFileSize()
    {
        var result = _updateFileSize.Invoke(_stream.Length);

        FileInfo = FileInfo.Copy().WithSize(result.Size);

        return result;
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}
