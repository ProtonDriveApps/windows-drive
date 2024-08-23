using System.Text.Json.Serialization;

namespace ProtonDrive.Client;

public sealed class MoveLinkParameters
{
    [JsonPropertyName("ParentLinkID")]
    public string? ParentLinkId { get; set; }

    public string? NodePassphrase { get; set; }

    public string? Name { get; set; }

    [JsonPropertyName("Hash")]
    public string? NameHash { get; set; }

    [JsonPropertyName("NameSignatureEmail")]
    public string? NameSignatureEmailAddress { get; set; }

    /// <summary>
    /// Current name hash before move operation. Used to prevent race conditions.
    /// </summary>
    [JsonPropertyName("OriginalHash")]
    public string? OriginalNameHash { get; init; }

    /// <summary>
    /// Destination Share ID, optional. Required in case of move between different remote shares.
    /// </summary>
    [JsonPropertyName("NewShareID")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NewShareId { get; set; }
}
