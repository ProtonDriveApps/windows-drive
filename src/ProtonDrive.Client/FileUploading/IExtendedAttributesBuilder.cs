using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proton.Security.Cryptography.Abstractions;

namespace ProtonDrive.Client.FileUploading;

internal interface IExtendedAttributesBuilder
{
    long? Size { get; set; }
    DateTime? LastWriteTime { get; set; }
    IEnumerable<int>? BlockSizes { get; set; }
    PublicPgpKey? NodeKey { get; init; }

    Task<string?> BuildAsync(CancellationToken cancellationToken);
}
