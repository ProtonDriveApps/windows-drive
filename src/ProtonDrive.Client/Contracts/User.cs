using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Contracts;

public sealed class User
{
    private IImmutableList<UserKey>? _keys;
    private long? _usedDriveSpace;
    private long? _maxDriveSpace;

    [JsonPropertyName("ID")]
    public string Id { get; set; } = string.Empty;

    public UserType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public long UsedSpace { get; set; }

    public long UsedDriveSpace
    {
        get => _usedDriveSpace ?? UsedSpace;
        set => _usedDriveSpace = value;
    }

    public long MaxSpace { get; set; }

    public long MaxDriveSpace
    {
        get => _maxDriveSpace ?? MaxSpace;
        set => _maxDriveSpace = value;
    }

    [JsonPropertyName("Private")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool IsPrivate { get; set; }

    [JsonPropertyName("Subscribed")]
    public int SubscriptionTier { get; set; }

    public int Services { get; set; }

    [JsonPropertyName("Delinquent")]
    public DelinquentState DelinquentState { get; set; }

    [JsonPropertyName("Email")]
    public string EmailAddress { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public IImmutableList<UserKey> Keys
    {
        get => _keys ??= ImmutableList<UserKey>.Empty;
        set => _keys = value;
    }

    public bool HasNoSubscription() => SubscriptionTier == 0; // Free user

    public bool IsDelinquent => DelinquentState is DelinquentState.Delinquent or DelinquentState.NotReceived;
}
