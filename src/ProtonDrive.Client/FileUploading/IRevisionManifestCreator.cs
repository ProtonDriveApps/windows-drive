using System;
using System.Collections.Generic;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.FileUploading;

internal interface IRevisionManifestCreator
{
    ReadOnlyMemory<byte> CreateManifest(IReadOnlyCollection<ITransferredBlock> blocks);
}
