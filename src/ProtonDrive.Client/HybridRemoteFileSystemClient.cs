using ProtonDrive.Shared.Features;
using ProtonDrive.Shared.IO;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.Client;

/// <summary>
/// A hybrid file system client that falls back to legacy implementation for functions not yet available in the SDK client.
/// </summary>
internal sealed class HybridRemoteFileSystemClient : IFileSystemClient<string>
{
    private readonly IFeatureFlagProvider _featureFlagProvider;
    private readonly IFileSystemClient<string> _legacyClient;
    private readonly IFileSystemClient<string> _sdkClient;

    public HybridRemoteFileSystemClient(
        IFeatureFlagProvider featureFlagProvider,
        IFileSystemClient<string> legacyClient,
        IFileSystemClient<string> sdkClient)
    {
        _featureFlagProvider = featureFlagProvider;
        _legacyClient = legacyClient;
        _sdkClient = sdkClient;
    }

    public void Connect(string syncRootPath, IFileHydrationDemandHandler<string> fileHydrationDemandHandler)
    {
        _legacyClient.Connect(syncRootPath, fileHydrationDemandHandler);
        _sdkClient.Connect(syncRootPath, fileHydrationDemandHandler);
    }

    public async Task DisconnectAsync()
    {
        await Task.WhenAll(
            _legacyClient.DisconnectAsync(),
            _sdkClient.DisconnectAsync()).ConfigureAwait(false);
    }

    public Task<IDestinationRevision<string>> CreateFileAsync(
        NodeInfo<string> info,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        return _sdkClient.CreateFileAsync(info, tempFileName, thumbnailProvider, fileMetadataProvider, progressCallback, cancellationToken);
    }

    public Task<IDestinationRevision<string>> CreateRevisionAsync(
        NodeInfo<string> info,
        long size,
        DateTime lastWriteTime,
        string? tempFileName,
        IThumbnailProvider thumbnailProvider,
        IFileMetadataProvider fileMetadataProvider,
        Action<Progress>? progressCallback,
        CancellationToken cancellationToken)
    {
        return _sdkClient.CreateRevisionAsync(
            info,
            size,
            lastWriteTime,
            tempFileName,
            thumbnailProvider,
            fileMetadataProvider,
            progressCallback,
            cancellationToken);
    }

    public Task<ISourceRevision> OpenFileForReadingAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        return _sdkClient.OpenFileForReadingAsync(info, cancellationToken);
    }

    public Task<NodeInfo<string>> GetInfoAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        return _legacyClient.GetInfoAsync(info, cancellationToken);
    }

    public IAsyncEnumerable<NodeInfo<string>> EnumerateAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        return _legacyClient.EnumerateAsync(info, cancellationToken);
    }

    public Task<NodeInfo<string>> CreateDirectoryAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        return _legacyClient.CreateDirectoryAsync(info, cancellationToken);
    }

    public Task MoveAsync(IReadOnlyList<NodeInfo<string>> sourceNodes, NodeInfo<string> destinationInfo, CancellationToken cancellationToken)
    {
        return _legacyClient.MoveAsync(sourceNodes, destinationInfo, cancellationToken);
    }

    public Task MoveAsync(NodeInfo<string> info, NodeInfo<string> destinationInfo, CancellationToken cancellationToken)
    {
        return _legacyClient.MoveAsync(info, destinationInfo, cancellationToken);
    }

    public Task DeleteAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        return _legacyClient.DeleteAsync(info, cancellationToken);
    }

    public Task DeletePermanentlyAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        return _legacyClient.DeletePermanentlyAsync(info, cancellationToken);
    }

    public Task DeleteRevisionAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        return _legacyClient.DeleteRevisionAsync(info, cancellationToken);
    }

    public void SetInSyncState(NodeInfo<string> info)
    {
        _sdkClient.SetInSyncState(info);
    }

    public Task HydrateFileAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        return _sdkClient.HydrateFileAsync(info, cancellationToken);
    }
}
