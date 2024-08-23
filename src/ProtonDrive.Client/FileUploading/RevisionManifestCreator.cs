using System;
using System.Collections.Generic;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.FileUploading;

internal class RevisionManifestCreator : IRevisionManifestCreator
{
    public ReadOnlyMemory<byte> CreateManifest(IReadOnlyCollection<ITransferredBlock> blocks)
    {
        var sortedBlocks = new SortedList<int, ITransferredBlock>(blocks.Count);

        var totalHashLength = 0;
        foreach (var block in blocks)
        {
            sortedBlocks.Add(block.Index, block);
            totalHashLength += block.Hash.Length;
        }

        // For files over 10 GB, this will go into the LOH
        var manifest = new byte[totalHashLength].AsMemory();
        var offset = 0;

        // The thumbnail also needs to be counted to match what the API counts and allow for comparisons
        foreach (var (_, block) in sortedBlocks)
        {
            block.Hash.CopyTo(manifest[offset..]);
            offset += block.Hash.Length;
        }

        return manifest;
    }
}
