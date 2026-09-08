using Proton.Drive.Sdk.Sync.Shared.Adapters;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Adapter;

internal sealed class FallbackFileRevisionProviderDecorator<TId, TAltId> : IFileRevisionProvider<TId>
    where TId : struct, IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly IFileRevisionProvider<TId> _decoratedInstance;
    private readonly ICopySourceRevisionProvider<TId, TAltId> _copySourceRevisionProvider;

    public FallbackFileRevisionProviderDecorator(
        IFileRevisionProvider<TId> decoratedInstance,
        ICopySourceRevisionProvider<TId, TAltId> copySourceRevisionProvider)
    {
        _decoratedInstance = decoratedInstance;
        _copySourceRevisionProvider = copySourceRevisionProvider;
    }

    public async Task<ISourceRevision> OpenFileForReadingAsync(TId id, long version, CancellationToken cancellationToken)
    {
        try
        {
            return await _decoratedInstance.OpenFileForReadingAsync(id, version, cancellationToken).ConfigureAwait(false);
        }
        catch (FileRevisionProviderException ex) when (ex.ErrorCode is FileSystemErrorCode.Partial)
        {
            // If the file is partial, it could be due to move was replaced with copying and deletion.
            // On-demand file system clients attempt hydrating the file before reading it, so this is
            // reached only when hydration is not possible, for example due to insufficient local free space.
            var result = await _copySourceRevisionProvider.OpenForReadingAsync(id, cancellationToken).ConfigureAwait(false);

            if (result is null)
            {
                throw;
            }

            return result;
        }
    }
}
