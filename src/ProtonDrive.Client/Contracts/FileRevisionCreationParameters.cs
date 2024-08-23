using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed class FileRevisionCreationParameters
{
    [JsonPropertyName("CurrentRevisionID")]
    public string? CurrentRevisionId { get; set; }

    [JsonPropertyName("ClientUID")]
    public string? ClientId { get; set; }
}
