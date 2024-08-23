using System;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client;

internal sealed record DownloadedBlock(int Index, ReadOnlyMemory<byte> Hash) : ITransferredBlock;
