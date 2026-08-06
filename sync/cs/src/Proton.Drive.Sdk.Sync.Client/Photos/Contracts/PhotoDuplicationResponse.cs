using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Photos.Contracts;

public sealed record PhotoDuplicationResponse : ApiResponse
{
    [JsonPropertyName("DuplicateHashes")]
    public IReadOnlyCollection<PhotoDuplicateDto> PhotoDuplicates { get; init; } = [];
}
