using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record RevisionCreationConflict
{
    [JsonPropertyName("ConflictLinkID")]
    public string LinkId { get; init; } = string.Empty;

    [JsonPropertyName("ConflictRevisionID")]
    public string? RevisionId { get; init; }

    [JsonPropertyName("ConflictDraftRevisionID")]
    public string? DraftRevisionId { get; init; }

    [JsonPropertyName("ConflictDraftClientUID")]
    public string? ClientId { get; init; }
}
