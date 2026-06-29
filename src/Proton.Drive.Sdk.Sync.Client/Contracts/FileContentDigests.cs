using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed class FileContentDigests
{
    /// <summary>
    /// SHA-1 hash of the file content, in lowercase hexadecimal format.
    /// </summary>
    [JsonPropertyName("SHA1")]
    public string? Sha1 { get; init; }
}
