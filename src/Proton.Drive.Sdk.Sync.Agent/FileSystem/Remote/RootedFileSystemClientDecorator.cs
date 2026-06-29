using System.Diagnostics.CodeAnalysis;
using Proton.Drive.Sdk.Sync.Shared.FileSystem;

namespace Proton.Drive.Sdk.Sync.Agent.FileSystem.Remote;

internal sealed class RootedFileSystemClientDecorator : FileSystemClientDecoratorBase<string>
{
    private readonly IRootDirectory<string> _rootDirectory;

    public RootedFileSystemClientDecorator(IRootDirectory<string> rootDirectory, IFileSystemClient<string> origin)
        : base(origin)
    {
        _rootDirectory = rootDirectory;

        if (IsDefault(_rootDirectory.Id))
        {
            throw new ArgumentException("Root folder identity value must be specified", nameof(rootDirectory));
        }
    }

    public override async Task<NodeInfo<string>> GetInfoAsync(NodeInfo<string> info, CancellationToken cancellationToken)
    {
        if (!IsRoot(info))
        {
            return await base.GetInfoAsync(info, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrEmpty(info.Path))
        {
            throw new ArgumentException("The root folder path must be empty", nameof(info));
        }

        // The request about the root node always succeeds, the response is crafted from known data.
        return NodeInfo<string>.Directory()
            .WithId(_rootDirectory.Id)
            .WithName(string.Empty);
    }

    private static bool IsDefault([NotNullWhen(false)] string? value)
    {
        return value is null || value.Equals(default);
    }

    private bool IsRoot(NodeInfo<string> info)
    {
        return (IsDefault(info.Id) && string.IsNullOrEmpty(info.Path)) ||
               (!IsDefault(info.Id) && info.Id.Equals(_rootDirectory.Id));
    }
}
