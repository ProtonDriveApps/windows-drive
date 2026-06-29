using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Shared.IO;
using static Vanara.PInvoke.CldApi;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Client;

internal sealed class OnDemandRevisionCreationProcess : IDestinationRevision<long>
{
    private readonly FileSystemFile _file;

    public OnDemandRevisionCreationProcess(NodeInfo<long> fileInfo, FileSystemFile file)
    {
        _file = file;
        FileInfo = fileInfo;
    }

    public NodeInfo<long> FileInfo { get; }

    public NodeInfo<long> BackupInfo { get; set; } = NodeInfo<long>.Empty();

    public bool ImmediateHydrationRequired => false;
    public bool ChecksumVerificationEnabled => false;
    public bool CanGetContentStream => false;

    public Stream GetContentStream()
    {
        throw new NotSupportedException();
    }

    public Task WriteContentAsync(Stream source, FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<NodeInfo<long>> FinishAsync(FileContentChecksum expectedChecksum, CancellationToken cancellationToken)
    {
        using var placeholderCreationInfo = FileInfo.ToPlaceholderCreationInfo();

        if (!_file.GetPlaceholderState().HasFlag(PlaceholderState.Placeholder))
        {
            _file.ConvertToPlaceholder(placeholderCreationInfo.Value, CF_CONVERT_FLAGS.CF_CONVERT_FLAG_MARK_IN_SYNC);
        }

        _file.UpdatePlaceholder(
            placeholderCreationInfo.Value.FsMetadata,
            CF_UPDATE_FLAGS.CF_UPDATE_FLAG_DEHYDRATE | CF_UPDATE_FLAGS.CF_UPDATE_FLAG_MARK_IN_SYNC);

        // Parent identity is not checked or obtained, therefore, we do not include it into the result
        return Task.FromResult(_file.ToNodeInfo(parentId: 0, refresh: true));
    }

    public void Dispose()
    {
        _file.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Dispose();

        return ValueTask.CompletedTask;
    }
}
