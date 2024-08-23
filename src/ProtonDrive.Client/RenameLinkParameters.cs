using System.Text.Json.Serialization;

namespace ProtonDrive.Client;

public sealed class RenameLinkParameters
{
    public string? Name { get; set; }

    [JsonPropertyName("Hash")]
    public string? NameHash { get; set; }

    [JsonPropertyName("NameSignatureEmail")]
    public string? NameSignatureEmailAddress { get; set; }

    [JsonPropertyName("MIMEType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaType { get; set; }

    /// <summary>
    /// Current name hash before move operation. Used to prevent race conditions.
    /// </summary>
    [JsonPropertyName("OriginalHash")]
    public string? OriginalNameHash { get; init; }
}
