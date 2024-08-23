using System;

namespace ProtonDrive.Client.FileUploading;

internal sealed class BlockVerificationFailedException : Exception
{
    public BlockVerificationFailedException(string shareId, string linkId, string revisionId, int blockIndex, Exception innerException)
        : base(
            $"Failed to generate verification token for block #{blockIndex} of revision with ID \"{revisionId}\" of file with link ID \"{linkId}\" on share with ID \"{shareId}\"",
            innerException)
    {
    }

    public BlockVerificationFailedException(string message)
        : base(message)
    {
    }

    public BlockVerificationFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public BlockVerificationFailedException()
    {
    }
}
