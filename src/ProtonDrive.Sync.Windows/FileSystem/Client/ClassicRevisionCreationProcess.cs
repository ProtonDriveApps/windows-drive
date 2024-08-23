using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Shared;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Sync.Windows.FileSystem.Client;

internal class ClassicRevisionCreationProcess : IRevisionCreationProcess<long>
{
    private readonly FileSystemFile _file;
    private readonly NodeInfo<long>? _initialInfo;
    private readonly NodeInfo<long> _finalInfo;
    private readonly Action<Progress>? _progressCallback;

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

    public Stream OpenContentStream()
    {
        try
        {
            var stream = new SafeFileStream(_file.OpenWrite(ownsHandle: false), FileInfo.Id);

            return _progressCallback is not null ? new ProgressReportingStream(stream, _progressCallback) : stream;
        }
        catch (Exception ex) when (ExceptionMapping.TryMapException(ex, FileInfo.Id, FileInfo.Id != default, out var mappedException))
        {
            throw mappedException;
        }
    }

    public Task<NodeInfo<long>> FinishAsync(CancellationToken cancellationToken)
    {
        _succeeded = true;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(FinishRevisionCreation());
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
    }

    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }

    protected virtual void OnReplacingOriginalFile(FileSystemFile originalFile, FileSystemFile tempFile)
    {
    }

    private NodeInfo<long> FinishRevisionCreation()
    {
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
                // Open and check the original file to ensure it exists and has not diverged metadata
                using var originalFile = _initialInfo.OpenAsFile(
                    FileMode.Open,
                    FileSystemFileAccess.ReadAttributes | FileSystemFileAccess.Delete,
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
            }

            _file.Rename(_finalInfo.Name, includeObjectId: false, replaceIfExists: _initialInfo != null && !backup);
        }

        return _file.ToNodeInfo(parentId: _finalInfo.ParentId, refresh: true);
    }
}
