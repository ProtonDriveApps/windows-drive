using System;

namespace ProtonDrive.Client.Contracts;

internal sealed record UploadedBlock(int Index, int Size, int NumberOfPlainDataBytesRead, ReadOnlyMemory<byte> Hash, UploadUrl UploadUrl, bool IsThumbnail)
    : ITransferredBlock;
