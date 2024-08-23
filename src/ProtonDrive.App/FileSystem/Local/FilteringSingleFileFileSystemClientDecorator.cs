using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Sync.Shared.FileSystem;

namespace ProtonDrive.App.FileSystem.Local;

public sealed class FilteringSingleFileFileSystemClientDecorator : FileSystemClientDecoratorBase<long>
{
    private readonly string _fileName;

    public FilteringSingleFileFileSystemClientDecorator(IFileSystemClient<long> instanceToDecorate, string fileName)
        : base(instanceToDecorate)
    {
        _fileName = fileName;
    }

    public override Task<NodeInfo<long>> GetInfo(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        if (!string.Equals(info.Name, _fileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new FileSystemClientException("Cannot get file info", FileSystemErrorCode.ObjectNotFound);
        }

        return base.GetInfo(info, cancellationToken);
    }

    public override IAsyncEnumerable<NodeInfo<long>> Enumerate(NodeInfo<long> info, CancellationToken cancellationToken)
    {
        return base.Enumerate(info, cancellationToken).Where(x => x.Name.Equals(_fileName, StringComparison.OrdinalIgnoreCase));
    }
}
