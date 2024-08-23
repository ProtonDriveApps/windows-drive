using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record RevisionCreationResponse : ApiResponse, IRevisionCreationConflictResponse
{
    private RevisionId? _revisionId;

    [JsonPropertyName("Revision")]
    public RevisionId RevisionId
    {
        get => _revisionId ??= new RevisionId();
        init => _revisionId = value;
    }

    [JsonPropertyName("Details")]
    public RevisionCreationConflict? Conflict { get; set; }
}
