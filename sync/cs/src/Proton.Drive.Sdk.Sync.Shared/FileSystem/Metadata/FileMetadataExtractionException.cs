using System.Diagnostics.CodeAnalysis;
using Proton.Drive.Shared.Extensions;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.Metadata;

public sealed class FileMetadataExtractionException : Exception, IFormattedErrorCodeProvider
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

    public bool TryGetRelevantFormattedErrorCode([MaybeNullWhen(false)] out string formattedErrorCode)
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
