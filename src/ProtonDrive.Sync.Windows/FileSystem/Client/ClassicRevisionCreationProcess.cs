using ProtonDrive.Shared;
using ProtonDrive.Shared.Extensions;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal class ClassicRevisionCreationProcess : IRevisionCreationProcess<long>
{
    private readonly FileSystemFile _file;
    private readonly NodeInfo<long>? _initialInfo;
    private readonly NodeInfo<long> _finalInfo;
    private readonly Action<Progress>? _progressCallback;

    private Stream? _contentWritingStream;
    private bool _succeeded;

    public ClassicRevisionCreationProcess(
        FileSystemFile file,
        NodeInfo<long>? initialInfo,
        NodeInfo<long> fileInfo,
        NodeInfo<long> finalInfo,
        Action<Progress>? progressCallback)
    {
        Ensure.NotNullOrEmpty(finalInfo.Name, nameof(finalInfo), nameof(finalInfo.Name));

        _file = file;
        _initialInfo = initialInfo;
        FileInfo = fileInfo;
        _finalInfo = finalInfo;
        _progressCallback = progressCallback;
    }

    public NodeInfo<long> FileInfo { get; }
    public NodeInfo<long> BackupInfo { get; set; } = NodeInfo<long>.Empty();
    public bool ImmediateHydrationRequired => true;
    public bool CanGetContentStream => true;

    public Stream GetContentStream()
    {
        if (_contentWritingStream is not null)
        {
            throw new InvalidOperationException("Content can be written only once");
        }

        try
        {
            var stream = new SafeFileStream(_file.OpenWrite(ownsHandle: false), FileInfo.Id);

            if (_finalInfo.Size >= 0)
            {
                // Reserve disk space to prevent failure while writing file content
                stream.SetLength(_finalInfo.Size);
            }

            _contentWritingStream = _progressCallback is not null ? new WriteOnlyProgressReportingStream(stream, _progressCallback) : stream;

            return _contentWritingStream;
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, FileInfo.Id, FileInfo.Id != 0, out var mappedException))
        {
            throw mappedException;
        }
    }

    public Task WriteContentAsync(Stream source, CancellationToken cancellationToken)
    {
        var destination = GetContentStream();
        return CopyFileContentAsync(destination, source, cancellationToken);
    }

    public async Task<NodeInfo<long>> FinishAsync(CancellationToken cancellationToken)
    {
        _succeeded = true;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_contentWritingStream is not null)
            {
                await _contentWritingStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            return FinishRevisionCreation();
        }
        catch
        {
            _succeeded = false;
            throw;
        }
    }

    public void Dispose()
    {
        if (!_succeeded)
        {
            _file.TryDelete();
        }

        _file.Dispose();
        _contentWritingStream?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }

    protected virtual void OnReplacingOriginalFile(FileSystemFile originalFile, FileSystemFile tempFile)
    {
    }

    private static async Task CopyFileContentAsync(Stream destination, Stream source, CancellationToken cancellationToken)
    {
        await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);

        // Destination should be flushed but not closed so that the local file remains locked.
        // It is needed to set last write time and read the file metadata before releasing the file lock.
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private NodeInfo<long> FinishRevisionCreation()
    {
        if (_contentWritingStream != null && _contentWritingStream.Position != _contentWritingStream.Length)
        {
            // Truncate the file to the real number of bytes written
            _contentWritingStream.SetLength(_contentWritingStream.Position);
        }

        _file.SetLastWriteTime(_finalInfo);
        _file.SetAttributes(_finalInfo);

        if (!string.Equals(Path.GetFileName(_file.FullPath), _finalInfo.Name, StringComparison.Ordinal))
        {
            var backup = !BackupInfo.IsEmpty;

            if (backup)
            {
                Ensure.NotNull(_initialInfo, nameof(_initialInfo), nameof(_initialInfo));
            }

            if (_initialInfo != null)
            {
                var overwritingReadOnly = _finalInfo.Attributes.HasFlag(FileAttributes.ReadOnly);

                // Open and check the original file to ensure it exists, is writable (unless overwriting read-only), and has not diverged metadata
                using var originalFile = _initialInfo.OpenAsFile(
                    FileMode.Open,
                    FileSystemFileAccess.ReadAttributes | FileSystemFileAccess.WriteAttributes | FileSystemFileAccess.Delete | (overwritingReadOnly ? default : FileSystemFileAccess.WriteData),
                    FileShare.Read | FileShare.Delete);

                originalFile.ThrowIfMetadataMismatch(_initialInfo);

                // Apply original file properties, if any, to the temp file
                OnReplacingOriginalFile(originalFile, _file);

                // Backup the original file if specified
                if (backup)
                {
                    var newName = BackupInfo.GetNameAndThrowIfInvalid();

                    originalFile.Rename(newName, includeObjectId: true);
                }
                else if (overwritingReadOnly)
                {
                    // Remove read-only attribute of the original file, as otherwise replacing the file would fail
                    originalFile.SetAttributes(_initialInfo.Copy().WithAttributes(originalFile.Attributes & ~FileAttributes.ReadOnly));
                }
            }

            _file.Rename(_finalInfo.Name, includeObjectId: false, replaceIfExists: _initialInfo != null && !backup);
        }

        return _file.ToNodeInfo(parentId: _finalInfo.ParentId, refresh: true);
    }
}
