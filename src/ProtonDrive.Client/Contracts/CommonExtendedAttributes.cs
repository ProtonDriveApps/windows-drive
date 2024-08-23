using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed class CommonExtendedAttributes
{
    public long? Size { get; set; }

    [JsonPropertyName("ModificationTime")]
    public DateTime? LastWriteTime { get; set; }

    public IEnumerable<int>? BlockSizes { get; set; }
}
