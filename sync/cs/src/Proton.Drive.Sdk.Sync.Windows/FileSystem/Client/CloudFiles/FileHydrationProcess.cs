using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.FileSystem.Integration;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Client.CloudFiles;

internal sealed class FileHydrationProcess<TId> : IFileHydrationDemand<TId>, IDisposable
    where TId : IEquatable<TId>
{
    private readonly CloudFilesDataTransferStream _stream;
    private readonly ILocalVolumeInfoProvider _localVolumeInfoProvider;
    private readonly Func<long, NodeInfo<TId>> _updateFileSize;

    private ChecksumVerifyingStream? _verifyingStream;

    public FileHydrationProcess(
        NodeInfo<TId> fileInfo,
        bool checksumVerificationEnabled,
        CloudFilesDataTransferStream stream,
        ILocalVolumeInfoProvider localVolumeInfoProvider,
        Func<long, NodeInfo<TId>> updateFileSize)
    {
        FileInfo = fileInfo;
        ChecksumVerificationEnabled = checksumVerificationEnabled;
        _stream = stream;
        _localVolumeInfoProvider = localVolumeInfoProvider;
        _updateFileSize = updateFileSize;
    }

    public NodeInfo<TId> FileInfo { get; private set; }
    public bool ChecksumVerificationEnabled { get; }
    internal bool ChecksumVerificationPerformed => _verifyingStream is not null;
    internal bool ChecksumVerificationFailed => _verifyingStream?.VerificationFailed == true;
    internal bool ExpectedChecksumVerified => _verifyingStream?.ExpectedChecksumVerified == true;

    public Stream GetHydrationStream(FileContentChecksum expectedChecksum)
    {
        if (expectedChecksum.Sha1 is null)
        {
            return _stream;
        }

        _verifyingStream = new ChecksumVerifyingStream(_stream, expectedChecksum.Sha1.Value, expectedChecksum.Sha1Verified);

        return _verifyingStream;
    }

    public NodeInfo<TId> UpdateFileSize()
    {
        var result = _updateFileSize.Invoke(_stream.Length);

        FileInfo = FileInfo.Copy().WithSize(result.Size);

        return result;
    }

    public void ThrowIfInsufficientLocalFreeSpace()
    {
        var directoryPath = Path.GetDirectoryName(FileInfo.Path);

        if (_localVolumeInfoProvider.HasInsufficientFreeSpace(directoryPath, FileInfo.Size))
        {
            throw new FileSystemClientException<TId>(
                $"Not enough free space to hydrate the file ({FileInfo.Size} B).",
                FileSystemErrorCode.FreeSpaceExceeded,
                FileInfo.Id);
        }
    }

    public void Dispose()
    {
        _stream.Dispose();
    }
}
