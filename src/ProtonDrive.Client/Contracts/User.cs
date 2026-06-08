using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed record User
{
    private readonly long? _usedDriveSpace;
    private readonly long? _maxDriveSpace;

    [JsonPropertyName("ID")]
    public required string Id { get; init; }

    public string? Name { get; init; }

    public string? DisplayName { get; init; }

    [JsonPropertyName("Email")]
    public required string EmailAddress { get; init; }

    public required UserType Type { get; init; }

    public long UsedSpace { get; init; }

    [JsonPropertyName("UsedDriveSpace")]
    public long SplitStorageUsedSpace
    {
        get => _usedDriveSpace ?? UsedSpace;
        init => _usedDriveSpace = value;
    }

    public ProductUsedSpace ProductUsedSpace { get; init; } = ProductUsedSpace.Empty;

    public long MaxSpace { get; init; }

    public long MaxDriveSpace
    {
        get => _maxDriveSpace ?? MaxSpace;
        init => _maxDriveSpace = value;
    }

    [JsonPropertyName("Private")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool IsPrivate { get; init; }

    [JsonPropertyName("Subscribed")]
    public int SubscriptionTier { get; init; }

    public int Services { get; init; }

    [JsonPropertyName("Delinquent")]
    public DelinquentState DelinquentState { get; init; }

    public IImmutableList<UserKey> Keys
    {
        get => field ??= ImmutableList<UserKey>.Empty;
        init;
    }

    public required string Currency { get; init; }

    public bool IsDelinquent => DelinquentState is DelinquentState.Delinquent or DelinquentState.NotReceived;

    public bool HasNoSubscription() => SubscriptionTier == 0; // Free user
}
