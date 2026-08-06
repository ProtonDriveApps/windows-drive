using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Albums.Contracts;

public sealed record AddedPhotoResponseList : ApiResponse
{
    [JsonPropertyName("Responses")]
    public IReadOnlyCollection<AddedPhotoWithLinkIdResponse> AddedPhotoResponses { get; init; } = [];
}
