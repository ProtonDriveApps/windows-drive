using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed class CommonExtendedAttributes
{
    public long? Size { get; set; }

    [JsonPropertyName("ModificationTime")]
    public DateTime? LastWriteTime { get; set; }

    public IEnumerable<int>? BlockSizes { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FileContentDigests? Digests { get; set; }
}
