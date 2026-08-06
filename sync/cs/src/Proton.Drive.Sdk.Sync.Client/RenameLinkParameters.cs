using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client;

public sealed class RenameLinkParameters
{
    public required string Name { get; init; }

    [JsonPropertyName("Hash")]
    public required string NameHash { get; init; }

    [JsonPropertyName("NameSignatureEmail")]
    public required string NameSignatureEmailAddress { get; init; }

    /// <summary>
    /// Media type of the file content. Null if node is a folder.
    /// </summary>
    [JsonPropertyName("MIMEType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaType { get; init; }

    /// <summary>
    /// Current name hash before move operation. Used to prevent race conditions.
    /// </summary>
    [JsonPropertyName("OriginalHash")]
    public string? OriginalNameHash { get; init; }
}
