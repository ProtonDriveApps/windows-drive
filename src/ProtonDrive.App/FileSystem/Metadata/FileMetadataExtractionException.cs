using System.Diagnostics.CodeAnalysis;
using ProtonDrive.Sync.Shared.FileSystem.Photos;

namespace ProtonDrive.App.FileSystem.Metadata;

public sealed class FileMetadataExtractionException : PhotoImportException
{
    public FileMetadataExtractionException()
    {
    }

    public FileMetadataExtractionException(string message)
        : base(message)
    {
    }

    public FileMetadataExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public FileMetadataExtractionException(string message, FileMetadataExtractionErrorCode extractionErrorCode)
        : base(message)
    {
        MetadataExtractionErrorCode = extractionErrorCode;
    }

    public FileMetadataExtractionErrorCode? MetadataExtractionErrorCode { get; }

    public override bool TryGetRelevantFormattedErrorCode([MaybeNullWhen(false)] out string formattedErrorCode)
    {
        if (MetadataExtractionErrorCode is null)
        {
            formattedErrorCode = null;
            return false;
        }

        formattedErrorCode = $"{MetadataExtractionErrorCode}";
        return true;
    }
}
