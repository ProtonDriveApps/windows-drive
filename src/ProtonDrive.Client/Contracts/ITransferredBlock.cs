using System;

namespace ProtonDrive.Client.Contracts;

internal interface ITransferredBlock
{
    int Index { get; }
    ReadOnlyMemory<byte> Hash { get; }
}
