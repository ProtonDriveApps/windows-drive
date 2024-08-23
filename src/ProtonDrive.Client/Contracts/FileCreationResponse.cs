using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record FileCreationResponse : ApiResponse, IRevisionCreationConflictResponse
{
    private FileRevisionId? _fileRevisionId;

    [JsonPropertyName("File")]
    public FileRevisionId FileRevisionId
    {
        get => _fileRevisionId ??= new FileRevisionId();
        init => _fileRevisionId = value;
    }

    [JsonPropertyName("Details")]
    public RevisionCreationConflict? Conflict { get; set; }
}
