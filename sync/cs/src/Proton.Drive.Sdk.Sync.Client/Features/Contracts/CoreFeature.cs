using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Features.Contracts;

internal sealed record CoreFeature
{
    public required string Code { get; init; }

    [JsonPropertyName("Type")]
    public required string ValueType { get; init; }

    public required int Value { get; init; }
}
